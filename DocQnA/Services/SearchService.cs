using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using System.Text;

namespace DocQnA.Services
{
    public class SearchService
    {
        private readonly string _searchEndpoint;
        private readonly string _searchApiKey;
        private readonly string _indexName;

        private readonly SearchClient _searchClient;
        private readonly OpenAiService _openAiService;

        public SearchService(IConfiguration configuration, OpenAiService openAiService)
        {
            _searchEndpoint = configuration["SearchService:Endpoint"]!;
            _searchApiKey = configuration["SearchService:ApiKey"]!;
            _indexName = configuration["SearchService:IndexName"]!;
            _searchClient = new SearchClient(new Uri(_searchEndpoint), _indexName, new AzureKeyCredential(_searchApiKey));
            _openAiService = openAiService;
        }

        public async Task<string> SearchIndexAsync(string question)
        {
            var options = new SearchOptions
            {
                QueryType = SearchQueryType.Full,
                Size = 50,
                Select = { "title", "chunk" },
                IncludeTotalCount = true,
            };
            //To use sematic search, uncomment
            //options.QueryType = SearchQueryType.Semantic;
            //options.SemanticSearch = new SemanticSearchOptions
            //{
            //    SemanticConfigurationName = "default",
            //    QueryCaption = new(QueryCaptionType.Extractive),
            //    SemanticQuery = question,
            //    QueryAnswer = new(QueryAnswerType.Extractive)
            //};

            SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>(question, options);
            StringBuilder contextBuilder = new StringBuilder();

            await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
            {
                if (result.Document.TryGetValue("title", out object title) &&
                    result.Document.TryGetValue("chunk", out object chunk))
                {
                    contextBuilder.AppendLine($"Title of document: {title}");
                    contextBuilder.AppendLine($"Search result: {chunk.ToString()}");
                    contextBuilder.AppendLine();
                }
            }

            var results = await _openAiService.GetAnswerAsync(contextBuilder.ToString(), question);

            return results;
        }

        //Triggers a reset/run function on the indexer
        //so the ai search has the latest changes
        public async Task TriggerIndexerAsync()
        {
            var indexerName = "doc-questioning-indexer";
            var indexerClient = new SearchIndexerClient(new Uri(_searchEndpoint), new AzureKeyCredential(_searchApiKey));
            try
            {
                await indexerClient.GetIndexerAsync(indexerName);
                await indexerClient.ResetIndexerAsync(indexerName);
                await indexerClient.RunIndexerAsync(indexerName);
                await Task.Delay(1500);
            }
            catch (RequestFailedException ex) when (ex.Status == 429)
            {
                Console.WriteLine("Failed to run indexer due to throttling: {0}", ex.Message);
            }
        }
    }
}