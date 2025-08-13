# SafariBooks .NET Downloader

Download and generate *EPUB* of your favorite books from [*O'Reilly Learning*](https://learning.oreilly.com) library using cookie-based authentication.

I'm not responsible for the use of this program, this is only for *personal* and *educational* purpose.  
Before any usage please read the *O'Reilly*'s [Terms of Service](https://learning.oreilly.com/terms/).

<a href='https://ko-fi.com/Y8Y0MPEGU' target='_blank'><img height='60' style='border:0px;height:60px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com'/></a>

## Overview

This is a .NET 8 console application that downloads books from O'Reilly Learning (Safari Books Online) and converts them to EPUB format using cookie-based authentication.

**Features:**
- Cookie-based authentication (no login credentials required in the app)
- Downloads book content, images, and CSS
- Generates valid EPUB files
- Kindle-compatible output option
- Docker support with Dev Container

---

## Requirements & Setup

### Prerequisites
- .NET 8.0 Runtime or Docker (compatible with .NET 9.0 when available)
- Valid O'Reilly Learning subscription
- `cookies.json` file with your session cookies

### Using .NET (Local Installation)

1. **Clone the repository:**
   ```bash
   git clone https://github.com/Krusty93/safaribooks.git
   cd safaribooks
   ```

2. **Build the application:**
   ```bash
   cd src/SafariBooksDownloader
   dotnet build -c Release
   ```

3. **Create cookies.json file:**
   - Log in to [O'Reilly Learning](https://learning.oreilly.com) in your browser
   - Extract cookies using browser developer tools
   - Create a `cookies.json` file in the application directory with the format:
   ```json
   {
     "sessionid": "your_session_id",
     "csrftoken": "your_csrf_token",
     "other_cookie": "other_value"
   }
   ```

### Using Docker

1. **Build the Docker image:**
   ```bash
   docker build -t safaribooks-downloader .
   ```

2. **Run with Docker:**
   ```bash
   docker run -v "$(pwd)/cookies.json:/app/cookies.json" \
              -v "$(pwd)/Books:/app/Books" \
              safaribooks-downloader <BOOK_ID>
   ```

### Using Dev Container

1. Open the project in Visual Studio Code
2. Install the "Dev Containers" extension
3. Open Command Palette (`Ctrl+Shift+P`)
4. Select "Dev Containers: Reopen in Container"
5. The environment will be automatically configured

---

## Usage

### Basic Usage
```bash
# Using .NET directly
dotnet run <BOOK_ID>

# Using published executable
./SafariBooksDownloader <BOOK_ID>

# Using Docker
docker run -v "$(pwd)/cookies.json:/app/cookies.json" \
           -v "$(pwd)/Books:/app/Books" \
           safaribooks-downloader <BOOK_ID>
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

### Finding the Book ID

The Book ID is the digits found in the O'Reilly Learning URL:
`https://learning.oreilly.com/library/view/book-name/XXXXXXXXXXXXX/`

For example, from this URL:
`https://learning.oreilly.com/library/view/test-driven-development-with/9781491958698/`

The Book ID would be: `9781491958698`

---

## Authentication Setup

### Creating cookies.json

1. **Log in to O'Reilly Learning** in your browser
2. **Open Developer Tools** (F12 or right-click → Inspect)
3. **Go to the Application/Storage tab**
4. **Navigate to Cookies** → `https://learning.oreilly.com`
5. **Copy the relevant cookies** (especially `sessionid`, `csrftoken`)
6. **Create cookies.json** in the same directory as the executable:

```json
{
  "sessionid": "your_session_id_value",
  "csrftoken": "your_csrf_token_value",
  "BrowserId": "your_browser_id",
  "optimizelyEndUserId": "your_optimizely_id"
}
```

**Note:** Cookie values and names may vary. Copy all cookies from the `learning.oreilly.com` domain for best compatibility.

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
SafariBooksDownloader --kindle <BOOK_ID>
```

This adds CSS rules that improve rendering of tables and code blocks on Kindle devices.

To convert for Kindle:
```bash
ebook-convert "input.epub" "output.azw3"
```

---

## Development

### Project Structure
```
src/
└── SafariBooksDownloader/
    ├── SafariBooksDownloader.csproj
    ├── Program.cs                    # Main application entry
    ├── Services/
    │   ├── ApiClient.cs             # O'Reilly API client
    │   ├── HtmlProcessor.cs         # HTML/XHTML processing
    │   └── EpubBuilder.cs           # EPUB generation
    └── Utils/
        └── PathUtils.cs             # File/path utilities
```

### Building from Source
```bash
cd src/SafariBooksDownloader
dotnet build -c Release
dotnet publish -c Release -o ../../dist
```

---

## Docker Commands

### Build
```bash
docker build -t safaribooks-downloader .
```

### Run
```bash
# Basic usage
docker run -v "$(pwd)/cookies.json:/app/cookies.json" \
           -v "$(pwd)/Books:/app/Books" \
           safaribooks-downloader <BOOK_ID>

# With Kindle optimization
docker run -v "$(pwd)/cookies.json:/app/cookies.json" \
           -v "$(pwd)/Books:/app/Books" \
           safaribooks-downloader --kindle <BOOK_ID>
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
1. Follow the existing code style
2. Add tests for new functionality
3. Update documentation as needed

---

*For any issues, please don't hesitate to open an issue on GitHub.*