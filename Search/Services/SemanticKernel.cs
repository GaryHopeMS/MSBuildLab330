﻿using Microsoft.SemanticKernel;
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
    readonly Kernel kernel;
    readonly AzureCosmosDBMongoDBMemoryStore memoryStore;
    readonly ISemanticTextMemory memory;

    private readonly string _simpleSystemPrompt = @"
        You are a cheerful intelligent assistant for the Cosmic Works Bike Company 
        You answer as truthfully as possible.
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
      GetSimpleChatCompletionAsync(string prompt)
    {

        ChatHistory chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage(_simpleSystemPrompt);
        chatHistory.AddUserMessage(prompt);

        try
        {
            OpenAIPromptExecutionSettings settings = new();
            settings.Temperature = 0.2;
            settings.MaxTokens = _maxCompletionTokens;
            settings.TopP = 0.7;
            settings.FrequencyPenalty = 0;
            settings.PresencePenalty = -2;

            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, settings);

            CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata["Usage"];

            string completion = result.Items[0].ToString();
            int tokens = completionUsage.CompletionTokens;

            return (
             response: result.Items[0].ToString(),
             promptTokens: completionUsage.PromptTokens,
             responseTokens: completionUsage.CompletionTokens
             );

        }
        catch (Exception ex)
        {

            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    public async Task<(string? response, int promptTokens, int responseTokens)>
        GetConversationChatCompletionAsync(List<Message> conversationMessages, string prompt)
    {

        ChatHistory chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage(_cosmicSystemPrompt);
        foreach (var message in conversationMessages)
        {
            chatHistory.AddUserMessage(message.Prompt);
            chatHistory.AddAssistantMessage(message.Completion);
        }
        chatHistory.AddUserMessage(prompt);


        //_logger.Log(LogLevel.Information, $"Prompt and context {_systemPrompt} {RAGContext}");

        try
        {
            OpenAIPromptExecutionSettings settings = new();
            settings.Temperature = 0.2;
            settings.MaxTokens = _maxCompletionTokens;
            settings.TopP = 0.7;
            settings.FrequencyPenalty = 0;
            settings.PresencePenalty = -2;

            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, settings);

            CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata["Usage"];

            string completion = result.Items[0].ToString();
            int tokens = completionUsage.CompletionTokens;

            return (
             response: result.Items[0].ToString(),
             promptTokens: completionUsage.PromptTokens,
             responseTokens: completionUsage.CompletionTokens
             );

        }
        catch (Exception ex)
        {

            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    public async Task<(string? response, int promptTokens, int responseTokens)>
        GetCosmicChatCompletionAsync(List<Message> conversationMessages, string RAGContext, string prompt)
    {

        ChatHistory chatHistory = new ChatHistory();

        //_logger.Log(LogLevel.Information, $"Prompt and context {_systemPrompt} {RAGContext}");

        chatHistory.AddSystemMessage(_cosmicSystemPrompt + RAGContext);
        foreach (var message in conversationMessages)
        {
            chatHistory.AddUserMessage(message.Prompt);
            chatHistory.AddAssistantMessage(message.Completion);
        }
        chatHistory.AddUserMessage(prompt);


        try
        {
            OpenAIPromptExecutionSettings settings = new();
            settings.Temperature = 0.2;
            settings.MaxTokens = _maxCompletionTokens;
            settings.TopP = 0.7;
            settings.FrequencyPenalty = 0;
            settings.PresencePenalty = -2;

            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, settings);

            CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata["Usage"];

            string completion = result.Items[0].ToString();
            int tokens = completionUsage.CompletionTokens;

            return (
             response: result.Items[0].ToString(),
             promptTokens: completionUsage.PromptTokens,
             responseTokens: completionUsage.CompletionTokens
             );

        }
        catch (Exception ex)
        {

            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }
    }

    public async Task<(string? response, int promptTokens, int responseTokens)> GetPreferredSourceAsync(string prompt)
    {
        ChatHistory sourceChatHistory = new ChatHistory();

        sourceChatHistory.AddSystemMessage(_sourceSelectionSystemPrompt);
        sourceChatHistory.AddUserMessage(prompt);

        try
        {

            OpenAIPromptExecutionSettings settings = new();

            settings.Temperature = 1.0;
            settings.MaxTokens = _maxCompletionTokens;
            settings.TopP = 1.0;
            settings.FrequencyPenalty = 0;
            settings.PresencePenalty = -2;

            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(sourceChatHistory, settings);

            CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata["Usage"];

            return (
             response: result.Items[0].ToString(),
             promptTokens: completionUsage.PromptTokens,
             responseTokens: completionUsage.CompletionTokens
            );

        }
        catch (Exception ex)
        {

            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;

        }

    }

    public async Task AddCachedMemory(string promptText, string completionText)
    {
        await memory.SaveInformationAsync("cache", promptText, Guid.NewGuid().ToString(), additionalMetadata: completionText);
    }

    public async Task<string> CheckCache(string userPrompt)
    {
        string cacheRestult = string.Empty;
        var memoryResults = memory.SearchAsync("cache", userPrompt, limit: 1, minRelevanceScore: 0.95);
        await foreach (var memoryResult in memoryResults)
        {
            cacheRestult = memoryResult.Metadata.AdditionalMetadata.ToString();
            break;
        }
        return cacheRestult;
    }

    public async Task ClearCacheAsync()
    {
        await memoryStore.DeleteCollectionAsync("cache");
    }

    /// <summary>
    /// Generates embeddings from the deployed OpenAI embeddings model using Semantic Kernel.
    /// </summary>
    /// <param name="input">Text to send to OpenAI.</param>
    /// <returns>Array of vectors from the OpenAI embedding model deployment.</returns>

    public async Task<(float[] vectors, int embeddingsTokens)> GetEmbeddingsAsync(string input)
    {

        float[] embedding = new float[0];
        int responseTokens = 0;
        try
        {

            var embeddings = await kernel.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingAsync(input);

            float[] embeddingsArray = embeddings.ToArray();


            // ToDo: how do I get the tokens 
            // responseTokens = embeddings.Usage.TotalTokens;
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


    /// <summary>
    /// Sends the existing conversation to the Semantic Kernel and returns a two word summary.
    /// </summary>
    /// <param name="sessionId">Chat session identifier for the current conversation.</param>
    /// <param name="conversationText">conversation history to send to Semantic Kernel.</param>
    /// <returns>Summarization response from the OpenAI completion model deployment.</returns>
    public async Task<string> SummarizeConversationAsync(string conversation)
    {
        //return await summarizePlugin.SummarizeConversationAsync(conversation, kernel);

        var skChatHistory = new ChatHistory();
        skChatHistory.AddSystemMessage(_summarizeSystemPrompt);
        skChatHistory.AddUserMessage(conversation);


        OpenAIPromptExecutionSettings settings = new();

        // settings.ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions;
        settings.Temperature = 0.0;
        settings.MaxTokens = 200;
        settings.TopP = 1.0;
        settings.FrequencyPenalty = -2;
        settings.PresencePenalty = -2;

        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        string completion = result.Items[0].ToString()!;
        string summary = Regex.Replace(completion, @"[^a-zA-Z0-9\s]", "");

        return summary;
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