# Glyphlog

One-line, emoji-tagged console logs and a startup banner for ASP.NET Core.

```text
🚀  MyApp API — ready
🔗  http://localhost:5000
📗  OpenAPI /openapi/v1.json  ·  Health /health
💾  Database myapp
🧭  Environment Development
🛑  Ctrl+C to shut down

🌐  Request starting HTTP/1.1 GET http://localhost:5000/orders
🔑  AuthenticationScheme: Cookies was successfully authenticated.
💾  Executed DbCommand (3ms) [Parameters=[], CommandType='Text', CommandTimeout='30'] SELECT ...
📦  Order 1042 created
🚧  Invoices[42] Payment gateway answered in 2.3 s
❌  Invoices[43] Payment gateway refused the charge
```

Instead of the default formatter's two lines per log (`info: Some.Long.Namespace.Class[0]` plus the indented
message), every log is one line led by an emoji: the **level** for warnings and errors, the **category** for
everything else.

## Install

```bash
dotnet add package Glyphlog
```

Targets .NET 10 and ASP.NET Core. It builds on the shared framework only — no extra dependency.

## Console formatter

```csharp
using Glyphlog;

builder.Logging.AddGlyphlog(o => o.Map("📦", "MyApp"));
```

`AddGlyphlog` replaces the console formatter and switches the console to UTF-8 (without it the Windows console eats
the emoji). Want it only while developing? Wrap the call in `if (builder.Environment.IsDevelopment())`.

| Icon | When |
|---|---|
| 🔥 ❌ 🚧 | Critical, Error, Warning — followed by the short category and the event id, dimmed |
| 🔍 | Debug, Trace |
| 💾 | `Microsoft.EntityFrameworkCore` |
| 🔑 | `Microsoft.AspNetCore.Authentication`, `Microsoft.AspNetCore.Authorization` |
| 🌐 | `Microsoft.AspNetCore`, `Microsoft.Extensions.Http`, `System.Net.Http` |
| 🚀 | `Microsoft.Hosting.Lifetime` |
| 💬 | anything else (`DefaultIcon`) |

### Your own categories

A key is either a **prefix** on whole namespace segments, or a **pattern** where each `*` stands for any run of
characters — handy when a subject is spread across namespaces (a controller, a service and a model that all deal
with the weather):

```csharp
builder.Logging.AddGlyphlog(o => o
    .Map("📦", "MyApp")             // everything of yours
    .Map("💰", "MyApp.Billing")     // a namespace
    .Map("⛅", "*Weather*")         // any category that mentions it
    .Map("🌌", "*.Engine.*"));      // anything under an Engine namespace
```

When several keys match, the one with the most literal characters wins, so the order you map them in never matters.
To make a pattern beat your own prefix, anchor it: `MyApp*Weather*` beats `MyApp`, which beats `*Weather*`.

`Map` refuses icons that would break the column: an emoji followed by the `U+FE0F` variation selector (⚠️ 🗄️ ℹ️)
takes one terminal cell instead of two. Prefer emoji that are colored by default (🚧 💾 💬).

Below warning, a message that already starts with its own emoji (a summary panel, say) goes out untouched.

Colors are written only when stdout is a terminal; redirected output (a container's log output, a cloud log stream,
a file) gets plain text.

### From appsettings.json

The categories, plus the timestamp and scopes of `ConsoleFormatterOptions`, bind from
`Logging:Console:FormatterOptions`. Code passed to `AddGlyphlog` runs after it and wins.

```json
{
  "Logging": {
    "Console": {
      "FormatterOptions": {
        "TimestampFormat": "HH:mm:ss",
        "IncludeScopes": true,
        "Categories": { "MyApp.Billing": "💰", "*Weather*": "⛅" }
      }
    }
  }
}
```

Icons that come from configuration are not checked by `Map`'s rule.

## Startup banner

The app's identity card, printed once the server is listening — only then are the bound addresses known:

```csharp
await app.StartAsync();
StartupBanner.Write(app, "MyApp API", ["OpenAPI /openapi/v1.json", "Health /health"],
    ("💾", connectionString is null ? null : "Database myapp"));
await app.WaitForShutdownAsync();
```

Or, keeping `app.Run()`:

```csharp
app.Lifetime.ApplicationStarted.Register(() => StartupBanner.Write(app, "MyApp API", []));
app.Run();
```

- `docs` go on a single line; an empty list omits it.
- Each extra line is `(icon, text)`; a null or blank text omits the line, so optional information needs no `if`.
- The fixed phrases follow the UI culture (Portuguese or English). Pass `BannerText.English`,
  `BannerText.Portuguese` or your own `new BannerText(...)` right after `app` to choose.

The banner replaces the four `Microsoft.Hosting.Lifetime` lines ("Now listening on…", "Application started"…), so
silence that category to avoid printing the same thing twice:

```json
{ "Logging": { "LogLevel": { "Microsoft.Hosting.Lifetime": "Warning" } } }
```

## License

[MIT](LICENSE)
