using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace DocQnA.Services
{
    public class BlobStorageService
    {
        private readonly SearchService _searchService;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;
        private readonly string _searchContainerName;
        private readonly string _blobConnectionString;

        public BlobStorageService(IConfiguration configuration, SearchService searchService)
        {
            _blobConnectionString = configuration["BlobStorage:BlobConnectionString"]!;
            _containerName = configuration["BlobStorage:BlobContainerName"]!;
            _searchContainerName = configuration["BlobStorage:SearchContainerName"]!;
            _blobServiceClient = new BlobServiceClient(_blobConnectionString);
            _searchService = searchService;
        }

        public async Task<string> UploadPdfAsync(IFormFile file)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(file.FileName);

            if (await blobClient.ExistsAsync())
                return string.Empty;

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
            };

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, uploadOptions);
            }

            return file.FileName;
        }

        public async Task MoveDocumentsToQueryContainerAsync(IEnumerable<string> blobNames)
        {
            var sourceContainerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var queryContainerClient = _blobServiceClient.GetBlobContainerClient(_searchContainerName);
            await queryContainerClient.CreateIfNotExistsAsync();

            foreach (var blobName in blobNames)
            {
                var sourceBlobClient = sourceContainerClient.GetBlobClient(blobName);
                var queryBlobClient = queryContainerClient.GetBlobClient(blobName);

                var copyOperation = await queryBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);

                while (true)
                {
                    var properties = await queryBlobClient.GetPropertiesAsync();
                    if (properties.Value.CopyStatus != CopyStatus.Pending)
                        break;
                    await Task.Delay(500);
                }
            }

            await _searchService.TriggerIndexerAsync();
        }

        public BlobClient GetDocumentBlob(string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            return containerClient.GetBlobClient(blobName);
        }

        public async Task DeletePdfAsync(string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task EmptyQueryContainerAsync()
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_searchContainerName);
            var deleteTasks = new List<Task>();

            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                deleteTasks.Add(blobClient.DeleteIfExistsAsync());
            }

            await Task.WhenAll(deleteTasks);
            await _searchService.TriggerIndexerAsync();
        }
    }
}