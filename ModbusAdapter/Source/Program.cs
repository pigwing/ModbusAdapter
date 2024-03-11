using ModbusAdapter;

var bld = WebApplication.CreateBuilder(args);
bld.Services
   .AddFastEndpoints()
   .SwaggerDocument();
bld.Services.AddModbusServer();
bld.Services.AddHostedService<ModbusScanHostedService>();

var app = bld.Build();
ServiceProviderAccessor.SetServiceProvider(app.Services);
app.UseFastEndpoints()
   .UseSwaggerGen();


await app.RunAsync();

public partial class Program { }