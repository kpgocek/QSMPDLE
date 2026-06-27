var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var database = postgres.AddDatabase("qsmpdle");

builder.AddProject<Projects.QSMPDLE_Web>("qsmpdle-web").WithReference(database).WaitFor(database);

builder.Build().Run();
