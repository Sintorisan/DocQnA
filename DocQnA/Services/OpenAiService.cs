using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace DocQnA.Services;

public class OpenAiService
{
    private readonly AzureOpenAIClient _azureClient;

    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deploymentName;

    public OpenAiService(IConfiguration configuration)
    {
        _endpoint = configuration["OpenAi:Endpoint"]!;
        _apiKey = configuration["OpenAi:ApiKey"]!;
        _deploymentName = configuration["OpenAi:DeploymentName"]!;

        _azureClient = new AzureOpenAIClient(new Uri(_endpoint), new AzureKeyCredential(_apiKey));
    }

    public async Task<string> GetAnswerAsync(string context, string question)
    {
        IChatClient chatClient = _azureClient
            .AsChatClient(_deploymentName);

        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "You are a helpful AI assistant that answers questions based on the provided context."),
            new ChatMessage(ChatRole.User, $"Context:\n{context}\n\nQuestion: {question}")
        };

        var chatResponse = await chatClient.GetResponseAsync(chatMessages);
        return chatResponse.Message.ToString();
    }
}