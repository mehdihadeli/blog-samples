var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.SbomDependencyTrackDemo_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.SbomDependencyTrackDemo_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
