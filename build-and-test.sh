#!/bin/bash

echo "SafariBooks .NET Downloader - Build and Test Script"
echo "=================================================="

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 8.0 or later."
    exit 1
fi

echo "✅ .NET SDK found: $(dotnet --version)"

# Navigate to project directory
cd "$(dirname "$0")/src/SafariBooksDownloader" || exit 1

# Clean and build the project
echo "🔧 Building project..."
dotnet clean > /dev/null 2>&1
if dotnet build -c Release > /dev/null 2>&1; then
    echo "✅ Build successful"
else
    echo "❌ Build failed"
    exit 1
fi

# Test help command
echo "🧪 Testing help command..."
if dotnet run -c Release -- --help > /dev/null 2>&1; then
    echo "✅ Help command works"
else
    echo "❌ Help command failed"
    exit 1
fi

# Test error handling (no cookies)
echo "🧪 Testing error handling..."
if dotnet run -c Release -- 123456 2>&1 | grep -q "unable to find"; then
    echo "✅ Error handling works"
else
    echo "❌ Error handling test failed"
    exit 1
fi

echo ""
echo "🎉 All tests passed! The SafariBooks .NET Downloader is ready to use."
echo ""
echo "Next steps:"
echo "1. Create a cookies.json file with your O'Reilly session cookies"
echo "2. Run: dotnet run <BOOK_ID> to download a book"
echo "3. Use --help for more options"