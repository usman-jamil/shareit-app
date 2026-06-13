using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
app.MapOpenApi();

app.UseHttpsRedirection();

var todos = new List<Todo>
{
    new(1, "Learn Nx", false),
    new(2, "Wire up OpenAPI types", false)
};

var group = app.MapGroup("/api/todos");

group.MapGet("/", () => todos)
     .WithName("GetTodos");

group.MapGet("/{id:int}", Results<Ok<Todo>, NotFound> (int id) =>
        todos.FirstOrDefault(t => t.Id == id) is { } todo
            ? TypedResults.Ok(todo)
            : TypedResults.NotFound())
     .WithName("GetTodoById");

group.MapPost("/", Results<Created<Todo>, BadRequest> (CreateTodo input) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
        return TypedResults.BadRequest();

    var todo = new Todo(todos.Count + 1, input.Title, false);
    todos.Add(todo);
    return TypedResults.Created($"/api/todos/{todo.Id}", todo);
})
.WithName("CreateTodo");

app.Run();

record Todo(int Id, string Title, bool Completed);
record CreateTodo(string Title);