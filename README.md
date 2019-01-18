# JoinPDF
C# Code to join two PDF's in one using simple C#, without external libraries.

Supports any PDF version. but it needs more testing, use it at your own risk.

##### Usage:

```csharp
JoinPDF joinPdf = new JoinPDF();            
byte[] joined = joinPdf.Join(
	File.ReadAllBytes("pdf1.pdf"), 
    File.ReadAllBytes("pdf2.pdf")
    );
```

##### Features

* Support XRef and XRefStream (PDF >= 1.5). XRefStream only with deflate algorithm, additionally it supports the PNG up predictor.
* Arrange and clean unused sections.
* Fast.
