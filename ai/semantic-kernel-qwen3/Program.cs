using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaChatCompletion(
    modelId: "qwen3:4b",
    endpoint: new Uri("http://localhost:11434")
);
Kernel kernel = kernelBuilder.Build();

var s = kernel.Services.GetRequiredService<IChatCompletionService>();
Console.ReadKey();