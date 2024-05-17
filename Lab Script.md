##Building AI Apps with Azure Cosmos DB for MongoDB, Azure OpenAI and the Semantic Kernel

## Virtual Machine Login

**Username**: +++@lab.VirtualMachine(Win11Base23B-W11-22H2).Username+++

**Password**: +++@lab.VirtualMachine(Win11Base23B-W11-22H2).Password+++

## Lab Introduction

In this hands-on lab we will show you how to design and build an application using Azure Cosmos DB for MongoDB. You will use Cosmos DB's new vector search capabilities combined with Azure OpenAI service to create a new contextually rich Generative AI chat experience based on the data you already have.

This hands-on lab will provide practical guidance on
on how to create a RAG pattern application over transactional data, generating vectors and completions with Azure OpenAI Service, connect to multiple vectorized data sources, manage chat history and enhance performance with a semantic cache all orchestrated using Semantic Kernel. 

This session will equip you with the knowledge you need to elevate your AI application development skills.


## What are we doing?

This lab guides you through the steps to implement Generative AI capabilities in an ASP.NET Core Blazor application utilizing the powerful vector search capabilities of Azure Cosmos DB for MongoDB and the power of Azure OpenAI LLMs. 
These are the main tasks you will accomplish in this lab.
1. Create a basic chat experience, forwarding user prompts to Azure OpenAI and surfacing the responses to the user interface, all the while storing this as chat history.
1. Implement and test a chat history feature to allow for more natural conversational interactions using the chat history for context.
1. Extend the functionality of your chat application based on the data you already have using Cosmos DB for MongoDB vector search capabilities to use this retrieved context as part of the RAG architecture pattern.
1.	(Optional) Implement and test a simple semantic cache for improved performance using the Semantic Kernel Connector for Cosmos DB for MongoDB.
1.	(Optional) Extend the chat application with some simple AI intelligence to select the appropriate data source for context retrieval.
1.	(Optional) Update the application user experience to provide summarized session navigation.

# Getting up and running 
In this lab you will be working on updating a pre-existing .NET AI assistant solution consisting of a single ASP.NET Blazor project. Whilst this project already provides a large amount of the boiler plate code typical of this type of application, you will still be building the core internals of this project to transform it into a fully functional AI assistant, using Azure Open AI and Cosmos DB for MongoDB services.

To save you time in this lab environment we have already:
1. Deployed the two Azure services you will need. 
1. Created a local code repository.
1. Created the database and collections with sample data.
1. Pre-loaded the appsettings file with the required settings and connection strings.

All these details are available in this labs' github repository.

###Getting going
So, let's have a quick look at the projects code and get started.
1.	Open **Visual Studio Code**, there is a link on the desktop or you can find it in the start menu. 
1.	Now open the project folder at **C:\Code\BuildLab330\SearchLab**, this is the app we are going to be working on. 

In a .NET application, it's common to use the configuration providers to inject new settings into your application. For this application we will use the **appsettings.json** file to provide the needed configuration values for Azure Cosmos DB and Azure OpenAI endpoints and keys.

4.	In the root of the project folder, open the file named **appsettings.json**.
4.	It should look similar to the following:

```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "OpenAi": {
    "Endpoint": "{YourOpenAIEnpoint}",
    "Key": "{YourOpenAIKey}",
    "EmbeddingsDeployment": "ada-002",
    "CompletionsDeployment": "gpt35",
    "MaxConversationTokens": "1500",
    "MaxContextTokens": "2000",
    "MaxCompletionTokens": "1500",
    "MaxEmbeddingTokens": "2000"
  },
  "MongoDb": {
    "Connection": "mongodb+srv://{YourMongoUsername}:{YourMongoKey}@{YourMonogClusterName}.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000",
    "DatabaseName": "retaildb",
    "CollectionNames": "products, customers, salesOrders, completions",
    "MaxVectorSearchResults": "50",
    "VectorIndexType": "hnsw"
  }
}
```
Here you can see in the **OpenAI section** of the file that we have an endpoint, model deployments and a key for your Azure Open AI service, along with some additional settings which we will use to control the behavior of our app (more on these later). 

In the **MongoDB section** you will see the prepopulated connection string for the Cosmos DB for MongoDB service, the database name, collection names and a couple of settings which will allow us to control some of the database access behavior.

You may want to come back later and tweak some of these settings

Its now time to make sure the starter application template works as expected. In this step we build the application to verify that there are no issues before we make any modifications.
Let’s build and run the application.
4.	Within **Visual Studio Code**, open a new terminal.
!IMAGE[LAB330ScreenShot0.png](instructions261180/LAB330ScreenShot0.png)

5.	Build the application using the **dotnet build** command.
```bash
dotnet build
```
6.	You should see a **Build succeeded** message and **0 Error(s)** when it completes.

You can expect to get a couple of warnings (yellow), these can be safely ignored for the demo code in this lab as you still need to complete some of it. 

7.	Run the application using the **dotnet run** command.
```bash
dotnet run
```
The output should look something similar to:
!IMAGE[LAB330ScreenShot0.1.png](instructions261180/LAB330ScreenShot0.1.png)
8.	You can **ctrl+click** on the URL that is part of **Now listening on http://localhost:8100** message you should see in the terminal window to open a browser and connect to the web application which is now running locally. Alternatively open the browser and navigate to http://localhost:8100

You now have the start of your new AI assistant up and running and it should look something like this:

!IMAGE[LAB330ScreenShot1.png](instructions261180/LAB330ScreenShot1.png)


####Let’s see what the bot can (or can't) do:
9.	Click the **“Create New Chat”** button, this will start a new chat session.
10.	Now say hi by typing `Hi there` in the message box and clicking the send button.
The chat bot will reply with a brief introduction: “I am a really friendly chat bot and super happy to meet you…”

No matter what you ask of it, it is going to respond the same way. 
It’s now your job to fix that. 
!IMAGE[LAB330ScreenShot2.png](instructions261180/LAB330ScreenShot2.png)

11. Close the browser and shut down the service by pressing Ctrl+C in the terminal window.

We are now ready to start building. 

Let's connect our application to the Azure Open AI Service to generate more useful responses from an LLM and convert our chat bot into an AI assistant.

===
# Making our AI assistant chat

Our application is composed of the following three services 
- The **ChatService** - this service is responsible for managing user interaction, chat logic and passing requests to the other services where appropriate.
- The **SemanticKernel** service - this service is responsible for managing the interaction between the Chat Service and the backend Azure OpenAI Service LLM. As a highly extensible SDK Semantic Kernel works with various different models by way of connectors and provides you with rich capabilities that allow you to build fully automated AI agents that can call your application code and automate automate your business processes. We will just be touching on the basic capabilities of semantic kernel in this lab.  
- The **MonogDBService** service - this services provides access to the data stored in the Cosmos DB for MongoDB database. The database will be used to store and retreive the chat history and provide aditional context to the chat based on the sample Cosmic Works retail dataset that it contains.


First things first, we need to have a look at the primary method that handles the main prompt response loop of our application. This is the method that is invoked when a user submits a prompt to the UI. 

1.	Open the **Services/ChatService.cs** file.
2.	Locate the **GetChatCompletionAsync** method and review the code. 

You will note the following about the GetChatCompletionAsync method:

- It takes parameters for the sessionId and prompt from the user interface 
- It generates responses (completions) to send back to the user.
- It stores both the prompt and completion in a new instance of the Message class.
- It stores this message in the database and updates the user interface by calling  AddPromptCompletionMessagesAsync method.

The applications primary behaviour is driven by the *prompt*, *completion* and *sessionId*. The AddPromptCompletionMessagesAsync method is used to store these key pieces of information in the database and make them available to the UI. 

You will use the other parameters passed to the method and store additional information in the message as this lab develops.

And as you can see from this part of the code:

```csharp

///// This is where the magic will happen        

// for now I am only good at introducing myself.
string completion = string.Empty;
completion = "I am a really friendly chat bot and super happy to meet you" +
        Environment.NewLine + "however I cant realy do anything for you";
```
It is not particularly useful at this point.

## Connecting to Azure Open AI with Semantic Kernel

Let us implement the Semantic Kernel service so we can generate more interesting responses from our chosen LLM. 

The first method we need to implement is to generate responses to our chat prompts, these responses are called completions. 
You will implement the method that calls Azure OpenAI Service using the Semantic Kernel model plugin for Azure OpenAI. This will allow the application to generate a chat completion from one of the deployed LLMs for a given user prompt. The method will need to return the generated response text as well as the number of tokens used for the prompt and the response (completion). 

### What are tokens and why do they matter
Large language model tokens refer to the basic units of text processed by the model. These tokens can represent words, parts of words or even punctuation marks, depending on the tokenization method used in that specific model. 
Tokenization is the process of breaking down text into these manageable pieces that allows the model to handle a wide variety of vocabulary and linguistic structures efficiently. In training the models learn to predict the next token in a sequence, given the previous tokens, which enables it to generate coherent and contextually appropriate text. This approach forms the backbone and magic of how models understand and generate human-like text, making tokenization a crucial aspect of natural language processing in AI. 
Tokens are used to meter, provision and rate limit access to large language models in order to manage and optimize resource allocation, ensure fair usage and control costs. Given this, a token is not just a unit of text (on average about 4 characters) but serves as a measure of the computational resources needed for processing of an input text or generating output text.

### Implementing GetChatCompletionAsync()

1. Open the **Services/SemanticKernel.cs** file.
2. On inspecting this class, you will see that there is already code providing the service with an initilized local instance of the Sematic Kernel (you dont need to change anything in this step)

```csharp
   //Semantic Kernel
   private readonly Kernel kernel;
```
```csharp
    // Initialize the Semantic Kernel
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddAzureOpenAIChatCompletion(
         semanticKernelOptions.CompletionsDeployment,
         semanticKernelOptions.Endpoint, 
         semanticKernelOptions.Key);
    kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
        semanticKernelOptions.EmbeddingsDeployment,
        semanticKernelOptions.Endpoint,
        semanticKernelOptions.Key);
    kernel = kernelBuilder.Build(); 
```
This code builds and initializes the kernel with the Azure OpenAI Chat Completion Model and Azure OpenAI Text Embedding Model connectors added to the build pipeline. Note that we are providing the connectors with the Endpoint, Key and Deployment from the configuration, so they have the information needed to connect to the service.

3. Locate the GetChatCompletionAsync method
```csharp
public async Task<(string? response,
     int promptTokens, 
     int responseTokens)>
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
    //....

```

4. Add the following code at top of the try block to create a new instance of the Semantic Kernal ChatHistory and add to this both the system prompt and the user prompt that was passed into the method:
```csharp
ChatHistory chatHistory = new ChatHistory();
chatHistory.AddSystemMessage(_simpleSystemPrompt);
chatHistory.AddUserMessage(prompt);
```
5. Add the following code to create an instance of the required settings that will be passed to the completion call:

```csharp

   OpenAIPromptExecutionSettings settings = new();
       settings.Temperature = 0.2;
       settings.MaxTokens = _maxCompletionTokens;
       settings.TopP = 0.7;
       settings.FrequencyPenalty = 0;
       settings.PresencePenalty = -2;
```

- The **Temperature** value controls the randomness of the completion. The higher the temperature, the more random the completion.
- The **MaxTokens** value controls the maximum number of tokens to generate in the completion.
- The **TopP** value controls the diversity of the completion.
- The **FrequencyPenalty** value controls the models' likelihood to repeat the same line verbatim.
- The **PresencePenalty** value controls likelihood of the model to talk about new topics.

1. Add the following code to call the connector for completion based on the chatHistory and settings provided:

```csharp
      var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, settings);
```

7. Replace the existing code with the following new code to extract the *response*, *promptTokens* and *completionTokens* from the *result* which will be returned to the caller as tuple.
```csharp
      // code to be replaced
      var response = ""; 
      var promptTokens = 0;
      var completionTokens = 0;
```
```csharp
    // new code
    var response = result.Items[0].ToString();
    CompletionsUsage completionUsage = 
        (CompletionsUsage)result.Metadata["Usage"];
    var promptTokens = completionUsage.PromptTokens;
    var completionTokens = completionUsage.CompletionTokens;
```
8. Thats it for the SemanticKernel service code so **save the Services/SemanticKernel.cs** file.

The updated GetChatCompletionAsync method should look like this:
```csharp
  GetChatCompletionAsync(string prompt)
    {

        try
        {
            //Call to Azure OpenAI to get response and tokens used 

            ChatHistory chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(_simpleSystemPrompt);
            chatHistory.AddUserMessage(prompt);

            OpenAIPromptExecutionSettings settings = new();
            settings.Temperature = 0.2;
            settings.MaxTokens = _maxCompletionTokens;
            settings.TopP = 0.7;
            settings.FrequencyPenalty = 0;
            settings.PresencePenalty = -2;

            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, settings);

            // new code
            var response = result.Items[0].ToString();
            CompletionsUsage completionUsage =
                (CompletionsUsage)result.Metadata["Usage"];
            var promptTokens = completionUsage.PromptTokens;
            var completionTokens = completionUsage.CompletionTokens;

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
```

9. Open the **Services/ChatService.cs** file.

10. Locate the **GetChatCompletionAsync** and replace the old code (that did very little), with this new code 
```csharp
// code to be replaced 

     // for now I am only good at introducing myself.
     string completion = string.Empty;
     completion = "I am a really friendly chat bot and super happy to meet you" +
             Environment.NewLine + "however I cant realy do anything for you";
```

```csharp
// new code
   string completion = string.Empty;
   (completion,  promptTokens,  completionTokens) =
       await _semanticKernelService.GetChatCompletionAsync(prompt);
```
8. That's it for the changes to chat service code so **save the Services/ChatService.cs** file.

The final version of the GetChatCompletionAsync should look like this:
```csharp
public async Task<string> GetChatCompletionAsync(string? sessionId, string prompt, string selectedCollectionName, string selectedCacheEnable)    {        try        {            ArgumentNullException.ThrowIfNull(sessionId);            // Setting some default values that will become more intersting to us later in the lab            bool cacheHit = false;            int promptTokens = 0;            int completionTokens = 0;            string collectionName = "none";            ///// This is where the magic will happen                    // new code            string completion = string.Empty;            (completion, promptTokens, completionTokens) =                await _semanticKernelService.GetChatCompletionAsync(prompt);            //Create message with all prompt, response and meta data            Message message = new Message(                    sessionId: sessionId,                    prompt: prompt,                    promptTokens: promptTokens,                    completion: completion,                    completionTokens: completionTokens,                    sourceSelected: selectedCollectionName,                    sourceCollection: collectionName,                    selectedCacheEnable, cacheHit);            //Commit message to array and database to drive the user experiance            await AddPromptCompletionMessagesAsync(sessionId, message);            return completion;        }        catch (Exception ex)        {            string message = $"ChatService.GetChatCompletionAsync(): {ex.Message}";            _logger.LogError(message);            throw;        }    }
```

9.	Within **Visual Studio Code**, open a new terminal.
10.	Build the application using the **dotnet build** command.
```bash
dotnet build
```
11.	You should see a **Build succeeded** message when it completes.

12.	Run the application using the **dotnet run** command and open your browser to http://localhost:8100
```bash
dotnet run
```
13. Lets ask our bot some general knowledge questions, like `what is the deepest ocean?`
!IMAGE[LAB330ScreenShot3.png](instructions261180/LAB330ScreenShot3.png)

Here you can see that the application is using the LLM to respond to individual prompts. Much more helpful.

===
# Giving our AI assistant context

We have the basics for our Generative AI chat application in place. Let's explore how well it responds to natural language interactions.

## Test contextual follow up questions

Humans interact with each other through conversations that have some *context* of what is being discussed. OpenAI's ChatGPT can also interact this way with humans. However, this capability is not native to an LLM itself. It must be implemented. Let's explore what happens when we test contextual follow up questions with our LLM where we ask follow up questions that imply an existing context like you would have in a conversation with another person.

1. If you shutdown the app open a new terminal and start the application using **dotnet run** and open your browser to http://localhost:8100

    ```bash
    dotnet run
    ```

1. In the web application, create a new chat session and ask the AI assistant the same question again, `What is the highest mountain in North America?`. And wait for the response, "Mount Denali, also known as Mount McKinley," with some additional information. 

    !IMAGE[LAB330ScreenShot4.png](instructions261180/LAB330ScreenShot4.png)

1. Ask this follow up question. `What is the second highest?`. The response generated should look like the one below and will either have nothing to do with your first question, or the LLM may respond it doesn't understand your question.

    !IMAGE[LAB330ScreenShot5.png](instructions261180/LAB330ScreenShot5.png)
What you are observing is LLM's are stateless. They do not by themselves maintain any conversation history and is missing the context necessary for the LLM to respond appropriately to your second question.

In this exercise we will show how to implement chat history, often called a **Conversation Context Window** for a Generative AI application. We will also explain the concept of tokens for an LLM and why these are important to consider when implementing a context window.

But before we write the code, we need to first understand the concept of tokens.

## Tokens and context

Large language models require chat history to generate contextually relevant results. But there is a limit how much text you can send. Large language models have limits on how much text they can process in a single request and output in a response. These limits are not measured in bytes but as **tokens**, which on  average represent about 4 characters. Tokens are essentially the compute currency for large language models. Because of this limit on tokens, it is  necessary for us to manage their use. This can be a bit tricky in certain scenarios. You will need to ensure enough context for the LLM to generate a correct response, while avoiding negative results of consuming too many tokens which can include incomplete results or unexpected behavior.

So to limit the maximum amount of chat history (and text) we send to our LLM, we will count the tokens for each user prompt and completion up to the amount specified in the **MaxConversationTokens** we specified in our configuration and passed to to the function.

## Building a context window using tokens

1. Within the **ChatService.cs** class locate the **GetConversationContext()** method. 
The **conversationMessages** variable in this function will contain the entire chat history for a specified session. The **trimmedMessages** is what we will use to construct a subset of those messages to send to the LLM to provide the appropriatly sized context based on tokens.

```csharp
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
```
It is important to note that in a conversation context recency matters, the most recent text is what we want closer to the actual question. To do that we will order the  **conversationMessages** in reverse TimeStamp order to select the most recent messages and count token usage to one message short of the **MaxConversationTokens** limit.


2. Beneath the creation of the ordered conversationMessages list and initalization of the trimmedMessages list add the following code.

```csharp
  int totalTokens = 0;
  int totalMessages = 0;

  foreach ( var message in conversationMessages)
  {
      var messageTokens = tokenizer.CountTokens(message.Prompt) + tokenizer.CountTokens(message.Completion);
      if ((totalTokens+ messageTokens)> maxConverstionTokens)
          break;
      totalMessages++;
      trimmedMessages.Add(message);
  }
```
As this code iterates through the messages and copies them from one list to the other we evaluate the tokens using a tokenizer. Using the tokenizers estimated token cost of each message, the sum of  the Prompt and the Completion tokens total tokens are calculated and store in totalTokens. At the point in the process at which the totalTokens plus the tokens for the next message will exceed the maxConverstionTokens we return the trimmedMessages in the appopriate asscending order and limited to the provided token limit.

Tokenizers are usefull to estimate the tokens in a manner consistent with how a specific model would tokenize but with the advantage that you dont need to call the LLM directly. In our implementation here we are using the Microsoft ML tokenizer for the GPT 3.5 Turbo model which you can see instanciated at the top of the class with the following code. 

```csharp
private readonly Tokenizer tokenizer = Tokenizer.CreateTiktokenForModel("gpt-3.5-turbo");
```

1. Open the **Services/SemanticKernel.cs** file. 
1. Locate the **GetChatCompletionAsync** method
```csharp
public async Task<(string? response,
     int promptTokens, 
     int responseTokens)>
  GetChatCompletionAsync(string prompt)
{
    try
    {
        ChatHistory chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(_simpleSystemPrompt);
        chatHistory.AddUserMessage(prompt);

\\...
```
3. You need replace the begining of the method.

Here we are changing the signature of the function to include an additional parameter to allow us to pass in the conversation context as a list of messages. 

These messages (prompts and completions) are then added to the chatHistory immediatly following system prompt in the order in which occured, finally followed by the the latest user prompt.

Replace the begining of the method with the following code  

```csharp
public async Task<(string? response, int promptTokens, int responseTokens)>
    GetChatCompletionAsync(List<Message> conversationMessages, string prompt)
{
    try
    {
        ChatHistory chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(_simpleSystemPrompt);
        foreach (var message in conversationMessages)
        {
            chatHistory.AddUserMessage(message.Prompt);
            chatHistory.AddAssistantMessage(message.Completion);
        }
        chatHistory.AddUserMessage(prompt);
\\...
```
the final code for the GetChatCompletionAsync method should look like this:
```csharp
public async Task<(string? response, int promptTokens, int responseTokens)>    GetChatCompletionAsync(List<Message> conversationMessages, string prompt){    try    {        ChatHistory chatHistory = new ChatHistory();        chatHistory.AddSystemMessage(_simpleSystemPrompt);        foreach (var message in conversationMessages)        {            chatHistory.AddUserMessage(message.Prompt);            chatHistory.AddAssistantMessage(message.Completion);        }        chatHistory.AddUserMessage(prompt);...            OpenAIPromptExecutionSettings settings = new();            settings.Temperature = 0.2;            settings.MaxTokens = _maxCompletionTokens;            settings.TopP = 0.7;            settings.FrequencyPenalty = 0;            settings.PresencePenalty = -2;            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, settings);            // new code            var response = result.Items[0].ToString();            CompletionsUsage completionUsage =                (CompletionsUsage)result.Metadata["Usage"];            var promptTokens = completionUsage.PromptTokens;            var completionTokens = completionUsage.CompletionTokens;            return (             response: response,             promptTokens: promptTokens,             responseTokens: completionTokens             );        }        catch (Exception ex)        {            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";            _logger.LogError(message);            throw;        }    }
```

4. Next, within the **ChatService.cs** class, locate **GetChatCompletionAsync()**. Identify within function the code where we make a call to _semanticKernelService.GetChatCompletionAsync(prompt) and add a call to GetConversationContext immediatly before this assigning the returned context messages to conversationContext. Update the call to GetChatCompletionAsync to now include the conversationContext as bellow.

 ```csharp
 // old code
 (completion, promptTokens, completionTokens) = 
    await _semanticKernelService.GetChatCompletionAsync(prompt);
```

```csharp
// new code
List<Message> conversationContext = GetConversationContext(sessionId,_semanticKernelService.MaxConversationTokens);

(completion,  promptTokens,  completionTokens) =
     await _semanticKernelService.GetChatCompletionAsync(
         conversationContext, prompt);
```

the final code for the GetChatCompletionAsync method should look like this:
```csharp
 public async Task<(string? response, int promptTokens, int responseTokens)>        GetChatCompletionAsync(List<Message> conversationMessages, string prompt)    {        try        {            ChatHistory chatHistory = new ChatHistory();            chatHistory.AddSystemMessage(_simpleSystemPrompt);            foreach (var message in conversationMessages)            {                chatHistory.AddUserMessage(message.Prompt);                chatHistory.AddAssistantMessage(message.Completion);            }            chatHistory.AddUserMessage(prompt);            OpenAIPromptExecutionSettings settings = new();            settings.Temperature = 0.2;            settings.MaxTokens = _maxCompletionTokens;            settings.TopP = 0.7;            settings.FrequencyPenalty = 0;            settings.PresencePenalty = -2;            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, settings);            // new code            var response = result.Items[0].ToString();            CompletionsUsage completionUsage =                (CompletionsUsage)result.Metadata["Usage"];            var promptTokens = completionUsage.PromptTokens;            var completionTokens = completionUsage.CompletionTokens;            return (             response: response,             promptTokens: promptTokens,             responseTokens: completionTokens             );        }        catch (Exception ex)        {            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";            _logger.LogError(message);            throw;        }    }
```
5. Save **ChatService.cs** and **SemanticKernel.cs**

6. If you have not shutdown your app do so now the terminal and start the application using **dotnet run** and open your browser to http://localhost:8100

7. Let see if adding context helped when we try those same questions again.
In the web application, create a new chat session and ask the AI assistant the same question again, `What is the highest mountain in North America?`. And wait for the response stating that it is Mount Denali, also known as Mount McKinley.

8. Now ask the follow up question. `What is the second highest?`. The response generated is now going to be in response the question with the context of mountains in North America and should be Mount Logan.

!IMAGE[LAB330ScreenShot6.png](instructions261180/LAB330ScreenShot6.png)


===
# Adding additional data for AI assitant context 

We now have a have an AI assistant chat application that can take into account the conversation context. We want more than that, we want our AI assistant to act with an understanding of our data - the data that lives in our applications. 

For the demo application we building today we are using the Cosmic Works retail data. In this dataset we have product, customer and sales data which is an adapted subset of the Adventure Works 2017 dataset for a retail Bike Shop that sells bicycles, biking accessories, components and clothing.

## What is RAG and why vector search
RAG is an acronym for Retrieval Augmented Generation, a fancy term for providing additional context data to a large language model to use when generating a response (a completion) to a user's natural language question (a prompt). The data used in this type of application can be of any kind. However, there is a limit to how much data can be sent due to the token limits discussed. 

We hope that this part of the lab will highlight some of the opportunities that this architecture pattern offers, exposes some of the challenges you may encounter and provides a simple example of how you can approach it practicaly.

Vector search plays a crucial role in the Retrieval-Augmented Generation (RAG) architecture pattern by enabling efficient and effective retrieval of relevant information from a large sets of data. This capability is fundamental to the performance and utility of RAG models, particularly in tasks requiring access to specific knowledge or detailed information. Here’s how vector search enables the RAG architecture patterns:

In the RAG architecture, both the input query (the prompt or promt and conversation context) and the records in the corpus are transformed into vectors in a high-dimensional space using embeddings. These embeddings are generated through models that capture the semantic meaning of texts, allowing the system to go beyond simple keyword matching.

Vector search utilizes these embeddings to perform semantic matching. By calculating the similarity between the vector of the query and the vectors of the documents allowing for the identification records that are contextually relevant to the query, even if they do not share exact keywords. This is particularly important for complex queries where contextual understanding is key to retrieving useful information.

Vector search engines, often backed by algorithms like approximate nearest neighbor (ANN) search, are designed to handle very large datasets efficiently. They can quickly sift through millions of document vectors to find the most relevant matches for a given query vector. This speed and scalability are essential for integrating retrieval into the generative process without significant delays and at resonable costs.

Vector search based architectecturs can be continuously updated with new records and optimized embedding techniques allowing the RAG model to remain effective over time and adapt to new information and evolving data landscapes.

## How do we build RAG patterns practicaly 

There are four key elements to the RAG pattern: generating vectors (embeddings) for our dataset, generating vectors on our prompt context, searching using the stored vectors to retreive appropriate context data, generating completions based on this context. 

In our sample dataset the emdedding vectors were generated when the data was inserted into each of the collections in Azure Cosmos DB for MongoDB and stored in a property called *embedding* that is used for vector searches. In order for vector search queries to peform efficiently we want to ensure that the vectors are indexed with an vector index.

Users ask natural language questions using the web-based chat user interface we have built so far. This prompt along with the conversation context will be vectorized and used to perform the vector search query against the data in the collections store in Azure Cosmos DB for MongoDB. The results of this query will be sent, along with some or all of the conversation context we previous generated to Azure OpenAI Service to generate a response back to the user. 

We will continue to store all user prompts and completion as messages.

## Extending our app to support RAG

#### Creating the promt embedding

Providing a method to generate embedding based on some string context is the first order of business to enable us to generate a search vector based on the conversation context and latest prompt. 

1. Open the **Services/SemanticKernel.cs** file.

2. Locate the **GetEmbeddingsAsync()** and replace the single statement assigning the embeddingsArray to an empty array with a call the the Semantic Kernel connector for embeddings and the conversion back to a vector array.

```csharp
          // code to replace
          float[] embeddingsArray = new float[0];
```
```csharp
         // new code
         var embeddings = await kernel.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingAsync(input);
         float[] embeddingsArray = embeddings.ToArray();

```

The final should look like
```csharp
public async Task<(float[] vectors, int embeddingsTokens)> GetEmbeddingsAsync(string input)    {        try        {                     // new code         var embeddings = await kernel.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingAsync(input);         float[] embeddingsArray = embeddings.ToArray();            int responseTokens = 0;            return (embeddingsArray, responseTokens);        }        catch (Exception ex)        {            string message = $"SemanticKernel.GetEmbeddingsAsync(): {ex.Message}";            _logger.LogError(message);            throw;        }    }
```

3. Thats it for the changes to chat Semantic Kernel service code so **save the Services/SemanticKernel.cs** file.

4. Within the **ChatService.cs** class, locate the now familiar **GetChatCompletionAsync** method. 
Identify the previously added call to GetConversationContext and add the following code 

```csharp
// code to replace
List<Message> conversationContext = GetConversationContext(sessionId,_semanticKernelService.MaxConversationTokens);
```

```csharp
// new code
List<Message> conversationContext = GetConversationContext(sessionId,_semanticKernelService.MaxConversationTokens);
var conversationContextString = string
       .Join(Environment.NewLine,
           conversationContext.Select(m => m.Prompt + Environment.NewLine + m.Completion)
           .ToArray());

   (float[] promptConversationVectors, int promptConversationTokens)
       = await _semanticKernelService.GetEmbeddingsAsync(conversationContextString + Environment.NewLine + prompt);
```
This code converts the conversation history message list to a string of concatated prompts and completions stored in conversationContextString.  
This string is then passed to the new GetEmbeddingsAsync method passing in the conversationContextString with the latest user prompt appended.

#### Performing the vector search

We now need to create a method to perform the vector search for our context records and in the same way we did for conversation context and  limit this result to a token limit that we specified in our configuration. 

5. Open the **Services/MongoDbService.cs** file and locate the VectorSearchAsync()  method. 
```csharp
       public async Task<string> VectorSearchAsync(string collectionName, string path, float[] embeddings, int maxTokens)
    {
        try
        {
            string resultDocuments = "[";

            // add code here

            resultDocuments = resultDocuments + "]";
            return resultDocuments;
        }
//...
```
This method accepts a collectionName, a propery path, an embeddings vector and the maxTokens that can be used by the result which is a string containing a JSON array of context data

6. Add the following code to create an instance of the collection we are going to be searching.

```csharp
  IMongoCollection<BsonDocument> collection = _database.GetCollection<BsonDocument>(collectionName);

```

7. Add the following code to convert the provided vector array into a BSON object the MongoDB SDKs can operate on.
```csharp
  var embeddingsArray = new BsonArray(embeddings.Select(e => new BsonDouble(Convert.ToDouble(e))));

```

8. Add the following code to construct the vector search query pipeline.
```csharp
BsonDocument[] pipeline = new BsonDocument[]
{
    new BsonDocument
    {
        {
            "$search", new BsonDocument
            {
                {
                    "cosmosSearch", new BsonDocument
                    {
                        { "vector", embeddingsArray },
                        { "path", path },
                        { "k", _maxVectorSearchResults }
                    }
                },
                { "returnStoredSource", true }
            }
        }
    },
    new BsonDocument
    {
        {
            "$project", new BsonDocument
            {
                {"_id", 0 },
                {path, 0 },
            }
        }
    }
};
Here we specify the vector we what to use to search, the path of the property that contains the vectors and k the number of results that want the vector search to return. 

It is important to note that k is the number of records returned from the database but due to the size variance of the documents themselves it does not directly correlate with token count. 

We are also removing the _id and vector properies from the result as they will add no value to the context for the LLM, and add significant token cost. 

```

9. Add the following code to execute the MongoDB query pipeline
```csharp
            List<BsonDocument> bsonDocuments = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            List<string> textDocuments = bsonDocuments.ConvertAll(bsonDocument => bsonDocument.ToString());
```
This query is executed in the same manner you would execute any other pipeline query using the MongoDB SDK. The results are convertedback to string list representation of the JSON documents. 

It is important to note that in a RAG data relavancy in reation to the prompt or conversation context often matters more than recency so we keep the messages ordered as they were returned by the query, in order of most relavent first.

10. Add the following code to select and transform the most relavant document content into a single large JSON array with the token count evaluated using a tokenizer with the process stopping just short of the **maxTokens** limit. 
```csharp
            var totalTokens = 0;
            var totalDocuments = 0;

            foreach (var document in textDocuments)
            {
                var tokens = _tokenizer.CountTokens(document);
                if ((totalTokens + tokens) > maxTokens)
                {
                    break;
                }
                totalTokens += tokens;
                totalDocuments += 1;
                resultDocuments = resultDocuments + "," + document;
            }

```
You should now be familiar with this algorithm as it is identical to the one we used for the chat history context. 

3. Thats it for the changes to the MongoDB service code so **save the Services/MongoDBService.cs** file.

4. Within the **ChatService.cs** class, locate the **GetChatCompletionAsync** method and identify the previously added call to GetEmbeddingsAsync after which add the following code 

```csharp
switch (selectedCollectionName)
 {
     case "<none>":
         collectionName = "none";
         break;

     case "<auto>":
         collectionName = "none";         
         break;

     default:
         collectionName = selectedCollectionName;
         break;
 }
```
This code takes the selected collection name that is passed in using the selectedCollectionName parameter. We filter the <none> and <auto> values otherwise setting the collection name to the collection passed in.   

 5. Add the following code to execute the vector qury if the collection is not "none" and store the result in the retrievedRAGContext variable.

```csharp
string retrievedRAGContext = "";
if (collectionName != "none")
   retrievedRAGContext
       = await _mongoDbService.VectorSearchAsync(
           collectionName
           "embedding",
           promptConversationVectors
           _semanticKernelService.MaxContextTokens);
```

#### Performing the chat completion

6. Open the **Services/SemanticKernel.cs** file and locate the GetChatCompletionAsync method 

7. You need replace the begining of the method.

Here we are changing the signature of the function to include an additional parameter to allow us to pass in the RAGConntext context in addition to the the previously added conversation messages.

Replace the begining of the method with the following code. 

```csharp
public async Task<(string? response, int promptTokens, int responseTokens)>
    GetCosmicChatCompletionAsync(List<Message> conversationMessages, string RAGContext, string prompt)
{
    try
    {
        ChatHistory chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(_cosmicSystemPrompt + RAGContext);
```
   
The final GetChatCompletionAsync should look like
```csharp
public async Task<(string? response, int promptTokens, int responseTokens)>        GetChatCompletionAsync(List<Message> conversationMessages, string prompt)    {        try        {            ChatHistory chatHistory = new ChatHistory();            chatHistory.AddSystemMessage(_simpleSystemPrompt);            foreach (var message in conversationMessages)            {                chatHistory.AddUserMessage(message.Prompt);                chatHistory.AddAssistantMessage(message.Completion);            }            chatHistory.AddUserMessage(prompt);            OpenAIPromptExecutionSettings settings = new();            settings.Temperature = 0.2;            settings.MaxTokens = _maxCompletionTokens;            settings.TopP = 0.7;            settings.FrequencyPenalty = 0;            settings.PresencePenalty = -2;            var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(chatHistory, settings);            // new code            var response = result.Items[0].ToString();            CompletionsUsage completionUsage =                (CompletionsUsage)result.Metadata["Usage"];            var promptTokens = completionUsage.PromptTokens;            var completionTokens = completionUsage.CompletionTokens;            return (             response: response,             promptTokens: promptTokens,             responseTokens: completionTokens             );        }        catch (Exception ex)        {            string message = $"OpenAiService.GetChatCompletionAsync(): {ex.Message}";            _logger.LogError(message);            throw;        }    }
``` 
8. Thats it for the changes to the Semantic Kernel service code so **save the Services/SemanticKernel.cs** file.

9. Within the **ChatService.cs** class, locate the now familiar **GetChatCompletionAsync** method. 

10. Replace the previous call to GetChatCompletionAsync with the following code

```csharp
// code to replace
(completion, promptTokens, completionTokens) =  
await _semanticKernelService.GetChatCompletionAsync(
    conversationContext, prompt);
```

```csharp
// new code 
(completion,  promptTokens,  completionTokens) =                    await _semanticKernelService.GetCosmicChatCompletionAsync(
    conversationContext, retrievedRAGContext, prompt);
```  
8. And finaly we are done with updating the Chat service code so **save the Services/ChatService.cs** file.

#### Bring our data to life

9. If you have not shutdown your app do so now the terminal and start the application using **dotnet run** and open your browser to http://localhost:8100

By default your chat should stil function the way it used and provide and support conversational context when the data source selector is set to 'none'

10. In the web application, create a new chat session and ask the AI assistant `Whats the deepest ocean?`. And wait for the response, "the Pacific Ocean" with some additional information. 

11. Ask this follow up question. `What the second deepest?`, just thowing in a small grammer mistake to make it a little harder. And the response is "the Atlantic Ocean"

!IMAGE[LAB330ScreenShot7.png](instructions261180/LAB330ScreenShot7.png)

12. Create a new chat session, set the data soure to **products** and lets ask it something about the products that Cosmic Works sells. `What mountain bikes are available?`

!IMAGE[LAB330ScreenShot8.png](instructions261180/LAB330ScreenShot8.png)

12. Create a new chat session, set the data soure to **customers** and lets ask it something about about a customer, maybe she just introduced herself and I want to send her an email `What is Nancy Hirota's email address` and lets follow that up with a questions that requires conversational context `what details do you have for her`

!IMAGE[LAB330ScreenShot9.png](instructions261180/LAB330ScreenShot9.png)

We have met one of our key objective we have an intelegent AI chat assistant that can leverage our data to provide contextualy aware answers.

Now it's not perfect, we still need to tell it which data sources to use and we can tackle that challenge in the future.


