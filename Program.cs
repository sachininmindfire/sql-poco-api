using SQLPocoAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // During development, allow any origin
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register services
builder.Services.AddScoped<IPocoGeneratorService, PocoGeneratorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS - Important: UseCors must be called before UseHttpsRedirection and other middleware
app.UseCors();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
