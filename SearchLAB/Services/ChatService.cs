using Search.Models;
using MongoDB.Driver;
using Search.Constants;
using Azure.AI.OpenAI;
using System.Collections;
using Microsoft.ML.Tokenizers;

namespace Search.Services;

public class ChatService
{
    /// <summary>
    /// All data is cached in the _sessions List object.
    /// </summary>
    private static List<Session> _sessions = new();

    private readonly MongoDbService _mongoDbService;
    private readonly SemanticKernelService _semanticKernelService;
    private readonly ILogger _logger;

    private readonly Tokenizer tokenizer = Tokenizer.CreateTiktokenForModel("gpt-3.5-turbo");

    public ChatService(MongoDbService mongoDbService, SemanticKernelService semanticKernelService, ILogger logger)
    {

        _mongoDbService = mongoDbService;
        _semanticKernelService = semanticKernelService;

        _logger = logger;
    }

    public async Task<string> GetChatCompletionAsync(string? sessionId, string prompt, string selectedCollectionName, string selectedCacheEnable)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            // Setting some default values that will become more intersting to us later in the lab
            bool cacheHit = false;
            int promptTokens = 0;
            int completionTokens = 0;
            string collectionName = "none";


            ///// This is where the magic will happen        
            
            // for now I am only good at introducing myself.
            string completion = string.Empty;
            completion = "I am a really friendly chat bot and super happy to meet you" +
                    Environment.NewLine + "however I cant realy do anything for you";
            

            //Create message with all prompt, response and meta data
            Message message = new Message(
                    sessionId: sessionId,
                    prompt: prompt,
                    promptTokens: promptTokens,
                    completion: completion,
                    completionTokens: completionTokens,
                    sourceSelected: selectedCollectionName,
                    sourceCollection: collectionName,
                    selectedCacheEnable, cacheHit);
            
            //Commit message to array and database to drive the user experiance
            await AddPromptCompletionMessagesAsync(sessionId, message);

            return completion;
        }
        catch (Exception ex)
        {
            string message = $"ChatService.GetChatCompletionAsync(): {ex.Message}";
            _logger.LogError(message);
            throw;
        }
    }

    
    private string GetConversationHistoryString(string sessionId, int turns)
    {

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        List<Message> conversationMessages = _sessions[index].Messages.ToList(); //make a full copy

        var trimmedMessages = conversationMessages
            .OrderByDescending(m => m.TimeStamp)
            .Take(turns)
            .Select(m => m.Prompt + Environment.NewLine + m.Completion)
            .ToList();

        trimmedMessages.Reverse();

        string conversation = string.Join(Environment.NewLine, trimmedMessages.ToArray());

        return conversation;
    }

    private List<Message> GetConversationContext(
        string sessionId, int maxConverstionTokens)
    {
        // conversationMessages contains an ordered list of all conversation messsages for a session
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);
        List<Message> conversationMessages = _sessions[index]
            .Messages
            .OrderByDescending(m => m.TimeStamp)
            .ToList();

        List<Message> trimmedMessages = new List<Message>();

        //<insert code here>

        return trimmedMessages.Reverse<Message>().ToList();

    }


    /// <summary>
    /// Returns list of chat session ids and names for left-hand nav to bind to (display Name and ChatSessionId as hidden)
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync()
    {
        return _sessions = await _mongoDbService.GetSessionsAsync();
    }

    /// <summary>
    /// Returns the chat messages to display on the main web page when the user selects a chat from the left-hand nav
    /// </summary>
    public async Task<List<Message>> GetChatSessionMessagesAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        List<Message> chatMessages = new();

        if (_sessions.Count == 0)
        {
            return Enumerable.Empty<Message>().ToList();
        }

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        if (_sessions[index].Messages.Count == 0)
        {
            // Messages are not cached, go read from database
            chatMessages = await _mongoDbService.GetSessionMessagesAsync(sessionId);

            // Cache results
            _sessions[index].Messages = chatMessages;
        }
        else
        {
            // Load from cache
            chatMessages = _sessions[index].Messages;
        }

        return chatMessages;
    }

    /// <summary>
    /// User creates a new Chat Session.
    /// </summary>
    public async Task CreateNewChatSessionAsync()
    {
        Session session = new();
        _sessions.Add(session);
        await _mongoDbService.InsertSessionAsync(session);
    }

    /// <summary>
    /// Rename the Chat Ssssion from "New Chat" to the summary provided by OpenAI
    /// </summary>
    public async Task RenameChatSessionAsync(string? sessionId, string newChatSessionName)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);
        _sessions[index].Name = newChatSessionName;
        await _mongoDbService.UpdateSessionAsync(_sessions[index]);
    }

    /// <summary>
    /// User deletes a chat session
    /// </summary>
    public async Task DeleteChatSessionAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);
        _sessions.RemoveAt(index);
        await _mongoDbService.DeleteSessionAndMessagesAsync(sessionId);
    }



    public async Task<string> SummarizeChatSessionNameAsync(string? sessionId, string sessionText)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        //
        string response = $"Chat {sessionId.Substring(sessionId.Length-12)}";

        await RenameChatSessionAsync(sessionId, response);

        return response;
    }

   
    /// <summary>
    /// Add user prompt and AI assistance response to the chat session message list object and insert into the data service as a transaction.
    /// </summary>
    private async Task AddPromptCompletionMessagesAsync(string sessionId, Message message)
    {

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        //Add prompt and completion to the cache
        _sessions[index].AddMessage(message);

        //Update session cache with tokens used
        _sessions[index].TokensUsed += message.PromptTokens + message.CompletionTokens;

        await _mongoDbService.UpsertSessionBatchAsync(session: _sessions[index], message: message);
    }

    public async Task ClearCacheAsync()
    {
       
    }
    

}