using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Azure.AI.OpenAI;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBMongoDB;

using Search.Options;
using Search.Models;

#pragma warning disable  CS8600, CS8602, CS8604 
#pragma warning disable SKEXP0010, SKEXP0001, SKEXP0020

namespace Search.Services;

public class SemanticKernelService
{

    private readonly string _embeddingsModelOrDeployment = string.Empty;
    private readonly string _completionsModelOrDeployment = string.Empty;

    private readonly int _maxCompletionTokens = default;
    private readonly int _maxContextTokens = default;
    private readonly int _maxConversationTokens = default;

    private readonly ILogger _logger;

    //Semantic Kernel
    private readonly Kernel kernel;
    private readonly AzureCosmosDBMongoDBMemoryStore memoryStore;
    private readonly ISemanticTextMemory memory;

    private readonly string _simpleSystemPrompt = @"
        You are cheerful an intelligent assistant for the Cosmic Works Bike Company 
        You try to answer as truthfully as possible
        ";

    private readonly string _cosmicSystemPrompt = @"
        You are an intelligent assistant for the Cosmic Works Bike Company. 
        You are designed to provide helpful answers to user questions about
        product, product category, customer and sales order information provided in JSON format in the below context information.

        Instuctions:
        When responding with any customer information always include the customerId in your response.

        Context information:";

    //System prompt to send with user prompts to instruct the model for summarization
    private readonly string _summarizeSystemPrompt = @"
        Summarize the text below in one or two words to use as a label in a button on a web page. Output words only. Summarize the text below here:" + Environment.NewLine;

    private readonly string _sourceSelectionSystemPrompt = @"
        Select which source of additional  information would be most usefull to answer the question provided from either
        product, customer and sales order information sources based on the prompt provided.

        The product source contains information about the products the following properties: category Id, categoryName, sku, productName, description, price and tags
        The customer source contains information about the customer and has the following properties: customerId, title, firstName, lastName, emailAddress,  phone Number, addresses and order creation Date
        The sales order source contains information about customer sales and has the following properties: customerId, order Date, ship Date, sku, name, price and quantity

        Instructions:
        - If you're unsure of an answer, you must say ""unknown"".
        - Always select salesOrder as the reponse when the question contains the words ""sales"", ""purchases"" or ""invoices""
        - Only provide a one word answer:
            ""products"" if the product source is prefered
            ""customers"" if the customer source is prefered
            ""salesOrders"" if the sales order source is prefered
            ""none"" 
            ""unknown"" if you are unsure.

        Text of the question is :";


    /// <summary>
    /// Creates a new instance of the Semantic Kernel.
    /// </summary>
    /// <param name="semanticKernelOptions">Endpoint URI.</param>
    /// <param name="key">Account key.</param>
    /// <param name="completionDeploymentName">Name of the deployed Azure OpenAI completion model.</param>
    /// <param name="embeddingDeploymentName">Name of the deployed Azure OpenAI embedding model.</param>
    /// <exception cref="ArgumentNullException">Thrown when endpoint, key, or modelName is either null or empty.</exception>
    /// <remarks>
    /// This constructor will validate credentials and create a Semantic Kernel instance.
    /// </remarks>
    /// 
    ///public SemanticKernelService(string endpoint, string key, string completionDeploymentName, string embeddingDeploymentName, ILogger logger)
    public SemanticKernelService(OpenAi semanticKernelOptions, MongoDb mongoDbOptions, ILogger logger)
    {

        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.Endpoint);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.Key);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.CompletionsDeployment);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.EmbeddingsDeployment);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.MaxCompletionTokens);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.MaxContextTokens);
        ArgumentNullException.ThrowIfNullOrEmpty(semanticKernelOptions.MaxContextTokens);


        ArgumentNullException.ThrowIfNullOrEmpty(mongoDbOptions.Connection);
        ArgumentNullException.ThrowIfNullOrEmpty(mongoDbOptions.DatabaseName);

        _maxCompletionTokens = int.TryParse(semanticKernelOptions.MaxCompletionTokens, out _maxCompletionTokens) ? _maxCompletionTokens : 0;
        _maxConversationTokens = int.TryParse(semanticKernelOptions.MaxConversationTokens, out _maxConversationTokens) ? _maxConversationTokens : 0;
        _maxContextTokens = int.TryParse(semanticKernelOptions.MaxContextTokens, out _maxContextTokens) ? _maxContextTokens : 0;

        _logger = logger;

        // Initialize the Semantic Kernel
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureOpenAIChatCompletion(semanticKernelOptions.CompletionsDeployment, semanticKernelOptions.Endpoint, semanticKernelOptions.Key);
        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(semanticKernelOptions.EmbeddingsDeployment, semanticKernelOptions.Endpoint, semanticKernelOptions.Key);
        kernel = kernelBuilder.Build();

        AzureCosmosDBMongoDBConfig memoryConfig = new(1536);
        memoryConfig.Kind = AzureCosmosDBVectorSearchType.VectorHNSW;

        memoryStore = new(mongoDbOptions.Connection, mongoDbOptions.DatabaseName, memoryConfig);

        memory = new MemoryBuilder()
                .WithAzureOpenAITextEmbeddingGeneration(
                    semanticKernelOptions.EmbeddingsDeployment,
                    semanticKernelOptions.Endpoint,
                    semanticKernelOptions.Key)
                .WithMemoryStore(memoryStore)
                .Build();
    }

    public async Task<(string? response, int promptTokens, int responseTokens)>
      GetChatCompletionAsync(string prompt)
    {

        try
        {
            //Call to Azure OpenAI to get response and tokens used 

            var response = ""; 
            var promptTokens = 0;
            var completionTokens = 0;

            return (
             response: response,
             promptTokens: promptTokens,
             responseTokens: completionTokens
             );

        }
        catch (Exception ex)
        {

            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    public async Task<(float[] vectors, int embeddingsTokens)> GetEmbeddingsAsync(string input)
    {

        float[] embedding = new float[0];
        int responseTokens = 0;
        try
        {

            float[] embeddingsArray = new float[1536];
            responseTokens = 0;

            return (embeddingsArray, responseTokens);
        }
        catch (Exception ex)
        {
            string message = $"SemanticKernel.GetEmbeddingsAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    public int MaxCompletionTokens
    {
        get => _maxCompletionTokens;
    }

    public int MaxContextTokens
    {
        get => _maxContextTokens;
    }

    public int MaxConversationTokens
    {
        get => _maxConversationTokens;
    }

};