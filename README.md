# SafariBooks .NET Downloader

*This project is a .NET 9 rewrite of the original [SafariBooks downloader](https://github.com/lorenzodifuccia/safaribooks) by [lorenzodifuccia](https://github.com/lorenzodifuccia). All credits for the original concept and implementation go to the original author.*

*This project was ported using GitHub Copilot in order to test its features and development flows. Given the educational purposes of this program, I am not responsible for its use. Before any usage please read the *O'Reilly*'s [Terms of Service](https://learning.oreilly.com/terms/).*

## Overview

Download and generate *EPUB* of your favorite books from [*O'Reilly Learning*](https://learning.oreilly.com) library (Kindle-compatible).

**Features:**
- Cookie-based authentication (no login credentials required in the app)
- Downloads book content, images, and CSS
- Generates valid EPUB files
- Kindle-compatible output option
- Docker support with Dev Container

---

## How to Use

### Authentication Setup

The application uses cookie-based authentication, which is simpler and more secure than credential handling:

1. **Log in to O'Reilly Learning** in your browser at [learning.oreilly.com](https://learning.oreilly.com)
2. **Open Developer Tools** (F12 or right-click → Inspect)
3. **Go to the Application/Storage tab**
4. **Navigate to Cookies** → `https://learning.oreilly.com`
5. **Copy the relevant cookies** (especially `sessionid`, `csrftoken`)
6. **Create cookies.json** file with the following format:

```json
{
  "sessionid": "your_session_id_value",
  "csrftoken": "your_csrf_token_value",
  "BrowserId": "your_browser_id",
  "optimizelyEndUserId": "your_optimizely_id"
}
```

**Note:** Cookie values and names may vary. Copy all cookies from the `learning.oreilly.com` domain for best compatibility.

### Finding the Book ID

The Book ID is the digits found in the O'Reilly Learning URL:
`https://learning.oreilly.com/library/view/book-name/XXXXXXXXXXXXX/`

For example, from this URL:
`https://learning.oreilly.com/library/view/test-driven-development-with/9781491958698/`

The Book ID would be: `9781491958698`

### Basic Usage with Docker

```bash
# Build the Docker image
docker build -t safaribooks-downloader .

# Download a book
docker run -v "$(pwd)/cookies.json:/app/cookies.json" \
           -v "$(pwd)/Books:/app/Books" \
           safaribooks-downloader <BOOK_ID>

# With Kindle optimization
docker run -v "$(pwd)/cookies.json:/app/cookies.json" \
           -v "$(pwd)/Books:/app/Books" \
           safaribooks-downloader --kindle <BOOK_ID>
```

### Command Options
```bash
SafariBooksDownloader [--kindle] [--preserve-log] [--help] <BOOK ID>

positional arguments:
  <BOOK ID>            Book digits ID found in the URL:
                       https://learning.oreilly.com/library/view/BOOKNAME/XXXXXXXXXXXXX/

optional arguments:
  --kindle             Add CSS rules that improve Kindle rendering (tables, pre).
  --preserve-log       Keep logs (no-op placeholder).
  --help               Show this help message.
```

---

## Development Setup

### Using Dev Container (Recommended)

The project includes a complete development environment configuration that works with any IDE supporting dev containers:

**Visual Studio Code:**
1. Install the "Dev Containers" extension
2. Open the project folder
3. Open Command Palette (`Ctrl+Shift+P`)
4. Select "Dev Containers: Reopen in Container"
5. The environment will be automatically configured with .NET 9 SDK

**JetBrains Rider/IntelliJ:**
1. Use the "Remote Development" feature
2. Select "Dev Container" option
3. Point to the project's `.devcontainer/devcontainer.json`

**Other IDEs:**
1. Use Docker directly with the dev container:
   ```bash
   docker build -f .devcontainer/Dockerfile -t safaribooks-dev .
   docker run -it -v "$(pwd):/workspaces/safaribooks" safaribooks-dev
   ```

The dev container includes:
- .NET 9 SDK
- All required extensions and tools
- Automatic project restoration

### Project Structure
```
src/
├── SafariBooksDownloader/
│   └── SafariBooksDownloader.csproj
└── SafariBooksDownloader.UnitTests/
    └── SafariBooksDownloader.UnitTests.csproj
```

### Running Tests

To run the unit tests during development:

```bash
# Run all tests
dotnet test src/SafariBooksDownloader.sln

# Run tests with detailed output
dotnet test src/SafariBooksDownloader.sln --verbosity normal

# Run only unit tests project
dotnet test src/SafariBooksDownloader.UnitTests/SafariBooksDownloader.UnitTests.csproj
```

The test suite includes comprehensive unit tests for:
- **PathUtils**: File name and XML ID sanitization logic
- **EpubBuilder**: EPUB file structure generation (OPF, NCX, manifest)
- **JsonUtil**: JSON parsing and property extraction utilities
- **JsonExtensions**: Additional JSON processing extension methods

Tests are automatically excluded from Docker builds to keep the production image minimal.

---

## Requirements

- Docker (for running the application)
- Valid O'Reilly Learning subscription
- `cookies.json` file with your session cookies

---

## Output

The application creates the following structure:

```
Books/
└── Book Title (BOOK_ID)/
    ├── BOOK_ID.epub           # Final EPUB file
    ├── OEBPS/
    │   ├── *.xhtml            # Chapter files
    │   ├── content.opf        # EPUB metadata
    │   ├── toc.ncx           # Table of contents
    │   ├── Styles/           # CSS files
    │   └── Images/           # Book images
    └── META-INF/
        └── container.xml     # EPUB container
```

---

## EPUB Conversion for E-Readers

The generated EPUB files are compatible with most e-readers. For optimal compatibility:

### Calibre Conversion
For best quality, convert the generated EPUB using [Calibre](https://calibre-ebook.com/):

```bash
ebook-convert "input.epub" "output.epub"
```

### Kindle Compatibility
Use the `--kindle` option for better Kindle compatibility:

```bash
docker run -v "$(pwd)/cookies.json:/app/cookies.json" \
           -v "$(pwd)/Books:/app/Books" \
           safaribooks-downloader --kindle <BOOK_ID>
```

This adds CSS rules that improve rendering of tables and code blocks on Kindle devices.

To convert for Kindle:
```bash
ebook-convert "input.epub" "output.azw3"
```

---

## Troubleshooting

### Common Issues

1. **Authentication Failed**
   - Verify `cookies.json` exists and contains valid cookies
   - Check if your O'Reilly session has expired
   - Log out and log back in to get fresh cookies

2. **Book Not Found**
   - Verify the Book ID is correct
   - Ensure you have access to the book with your subscription
   - Check if the book URL is accessible when logged in

3. **Download Errors**
   - Check your internet connection
   - Verify O'Reilly Learning is accessible
   - Try again later if the service is temporarily unavailable

### Docker Issues

1. **Volume Mount Problems**
   ```bash
   # Ensure absolute paths
   docker run -v "$(pwd)/cookies.json:/app/cookies.json" \
              -v "$(pwd)/Books:/app/Books" \
              safaribooks-downloader <BOOK_ID>
   ```

2. **Permission Issues**
   - Ensure the local directories have proper permissions
   - On Linux/macOS, you might need to adjust file ownership after download

---

## Legal Notice

This tool is for personal and educational use only. Please:
- Respect O'Reilly's Terms of Service
- Only download books you have legitimate access to
- Do not redistribute downloaded content
- Support O'Reilly by maintaining your subscription

---

## Contributing

Feel free to open issues or submit pull requests. When contributing:
1. Use the dev container environment for consistency
2. Follow the existing code style
3. Add tests for new functionality
4. Update documentation as needed

---

*For any issues, please don't hesitate to open an issue on GitHub.*