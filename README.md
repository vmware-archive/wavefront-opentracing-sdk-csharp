# wavefront-opentracing-csharp-sdk

This .NET library provides open tracing support for Wavefront.

## Dependencies
  * .NET Standard (>= 2.0)
  * OpenTracing (>= 0.12.0)
  * System.Collections.Immutable (>= 1.5.0)
  * Wavefront.CSharp.SDK (>= 0.3.0-alpha) (https://github.com/wavefrontHQ/wavefront-csharp-sdk)

## Usage

### Tracer
OpenTracing Tracer is a simple, thin interface for Span creation and propagation across arbitrary transports.

#### How to instantiate a Wavefront tracer?
```csharp
var tracer = new WavefrontTracer.Builder().WithReporter(reporter).Build();
```

#### Close the tracer
Before exiting your application, don't forget to close the tracer which will flush all the buffered spans to Wavefront.
```csharp
tracer.Close();
```

When you instantiate the tracer, the builder pattern can be used to customize the reporter as shown below.

### WavefrontSender
Before we instantiate the Wavefront opentracing span reporter, we need to instantiate an IWavefrontSender 
(i.e. either WavefrontProxyClient or WavefrontDirectIngestionClient)
Refer to this page (https://github.com/wavefrontHQ/wavefront-csharp-sdk/blob/master/README.md)
to instantiate WavefrontProxyClient or WavefrontDirectIngestionClient.

### Option 1 - Proxy reporter using proxy WavefrontSender
```csharp
/* Report opentracing spans to Wavefront via a Wavefront Proxy */
var proxyReporter = new WavefrontSpanReporter.Builder()
  .WithSource("wavefront-tracing-example")
  .Build(proxyWavefrontSender);

/* Construct Wavefront opentracing Tracer using proxy reporter */
var tracer = new WavefrontTracer.Builder().WithReporter(proxyReporter).Build();  

/*  To get failures observed while reporting */
int totalFailures = proxyReporter.GetFailureCount();
```

### Option 2 - Direct reporter using direct ingestion WavefrontSender
```csharp
/* Report opentracing spans to Wavefront via Direct Ingestion */
var directReporter = new WavefrontSpanReporter.Builder()
  .WithSource("wavefront-tracing-example")
  .Build(directWavefrontSender);

/* Construct Wavefront opentracing Tracer using direct ingestion reporter */
var tracer = new WavefrontTracer.Builder().WithReporter(directReporter).Build();

/* To get failures observed while reporting */
int totalFailures = directReporter.GetFailureCount();
```

### Composite reporter (chaining multiple reporters)
```csharp
/* Creates a console reporter that reports span to stdout (useful for debugging) */
var consoleReporter = new ConsoleReporter("sourceName");

/* Instantiate a composite reporter composed of console and direct reporter */
var compositeReporter = new CompositeReporter(directReporter, consoleReporter);

/* Construct Wavefront opentracing Tracer composed of console and direct reporter */
var tracer = new WavefrontTracer.Builder().WithReporter(compositeReporter).Build();
```
