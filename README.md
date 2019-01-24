# wavefront-opentracing-sdk-csharp [![OpenTracing Badge](https://img.shields.io/badge/OpenTracing-enabled-blue.svg)](http://opentracing.io) [![travis build status](https://travis-ci.com/wavefrontHQ/wavefront-opentracing-sdk-csharp.svg?branch=master)](https://travis-ci.com/wavefrontHQ/wavefront-opentracing-sdk-csharp) [![NuGet](https://img.shields.io/nuget/v/Wavefront.OpenTracing.SDK.CSharp.svg)](https://www.nuget.org/packages/Wavefront.OpenTracing.SDK.CSharp)

The Wavefront by VMware OpenTracing SDK for C# is a library that provides OpenTracing support for Wavefront.

## Dependencies
  * .NET Standard (>= 2.0)
  * OpenTracing (>= 0.12.0)
  * Wavefront.SDK.CSharp (>= 1.0.0) ([NuGet](https://www.nuget.org/packages/Wavefront.SDK.CSharp/))
  * Wavefront.AppMetrics.SDK.CSharp (>= 2.0.1) ([NuGet](https://www.nuget.org/packages/Wavefront.AppMetrics.SDK.CSharp/))

## Set Up a Tracer
[Tracer](https://github.com/opentracing/specification/blob/master/specification.md#tracer) is an OpenTracing [interface](https://github.com/opentracing/opentracing-csharp#initialization) for creating spans and propagating them across arbitrary transports.

This SDK provides a `WavefrontTracer` for creating spans and sending them to Wavefront. The `WavefrontTracer` also automatically generates and reports [metrics and histograms](https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp/blob/master/docs/metrics.md) from your spans. The steps for creating a `WavefrontTracer` are:
1. Create an `ApplicationTags` instance, which specifies metadata about your application.
2. Create an `IWavefrontSender` instance for sending data to Wavefront.
3. Create a `WavefrontSpanReporter` for reporting trace data to Wavefront.
4. Create the `WavefrontTracer` instance.

The following code sample creates a Tracer. For the details of each step, see the sections below.

```csharp
Tracer CreateWavefrontTracer(string application, string service) {
  // Step 1. Create ApplicationTags. 
  ApplicationTags applicationTags = new ApplicationTags.Builder(application, service).Build();
  
  // Step 2. Create an IWavefrontSender instance for sending trace data via a Wavefront proxy.
  //         Assume you have installed and started the proxy on <proxyHostname>.
  IWavefrontSender wavefrontSender = new WavefrontProxyClient.Builder(<proxyHostname>)
    .MetricsPort(2878).TracingPort(30000).Build();
        
  // Step 3. Create a WavefrontSpanReporter for reporting trace data that originates on <sourceName>.
  IReporter wfSpanReporter = new WavefrontSpanReporter.Builder()
    .WithSource(<sourceName>).Build(wavefrontSender);
        
  // Step 4. Create the WavefrontTracer.
  return new WavefrontTracer.Builder(wfSpanReporter, applicationTags).Build();
}
```

### 1. Set Up Application Tags

Application tags determine the metadata (span tags) that are included with every span reported to Wavefront. These tags enable you to filter and query trace data in Wavefront.

You encapsulate application tags in an `ApplicationTags` object.
See [Instantiating ApplicationTags](https://github.com/wavefrontHQ/wavefront-sdk-csharp/blob/master/docs/apptags.md) for details.

### 2. Set Up an IWavefrontSender

An `IWavefrontSender` object implements the low-level interface for sending data to Wavefront. You can choose to send data to Wavefront using either the [Wavefront proxy](https://docs.wavefront.com/proxies.html) or [direct ingestion](https://docs.wavefront.com/direct_ingestion.html).

* If you have already set up an `IWavefrontSender` for another C# SDK that will run in the same process, use that one.  (For details about sharing a single instance of `IWavefrontSender` instance across SDKs, see [Share an IWavefrontSender Instance](https://github.com/wavefrontHQ/wavefront-sdk-csharp/blob/master/docs/sender.md#share-an-iwavefrontsender-instance).

* Otherwise, follow the steps in [Set Up an IWavefrontSender Instance](https://github.com/wavefrontHQ/wavefront-sdk-csharp/blob/master/docs/sender.md#set-up-an-iwavefrontsender-instance).


### 3. Set Up a Reporter
You must create a `WavefrontSpanReporter` to report trace data to Wavefront. You can optionally create a `CompositeReporter` to send data to Wavefront and to print to the console.

#### Create a WavefrontSpanReporter
To build a `WavefrontSpanReporter`, you must specify an `IWavefrontSender`. You can optionally specify a string that represents the source for the reported spans. If you omit the source, the host name is automatically used.

To create a `WavefrontSpanReporter`:

```csharp
// Create a WavefrontProxyClient or WavefrontDirectIngestionClient
IWavefrontSender sender = BuildWavefrontSender(); // pseudocode; see above

IReporter wfSpanReporter = new WavefrontSpanReporter.Builder()
  .WithSource("wavefront-tracing-example") // optional nondefault source name
  .Build(sender);

//  To get the number of failures observed while reporting
int totalFailures = wfSpanReporter.GetFailureCount();
```
**Note:** After you initialize the `WavefrontTracer` with the `WavefrontSpanReporter` (below), completed spans will automatically be reported to Wavefront.
You do not need to start the reporter explicitly.

#### Create a CompositeReporter (Optional)

A `CompositeReporter` enables you to chain a `WavefrontSpanReporter` to another reporter, such as a `ConsoleReporter`. A console reporter is useful for debugging.

```csharp
// Create a console reporter that reports span to console
IReporter consoleReporter = new ConsoleReporter("wavefront-tracing-example"); // Specify the same source you used for the WavefrontSpanReporter

// Instantiate a composite reporter composed of a console reporter and a WavefrontSpanReporter
IReporter compositeReporter = new CompositeReporter(wfSpanReporter, consoleReporter);

```

### 4. Create a WavefrontTracer
To create a `WavefrontTracer`, you pass the `ApplicationTags` and `Reporter` instances you created above to a Builder:

```csharp
ApplicationTags appTags = BuildTags(); // pseudocode; see above
IReporter wfSpanReporter = BuildReporter();  // pseudocode; see above
WavefrontTracer.Builder wfTracerBuilder = new WavefrontTracer.Builder(wfSpanReporter, appTags);
// Optionally configure sampling and add multi-valued span tags before building
ITracer tracer = wfTracerBuilder.Build();
```

#### Sampling (Optional)
You can optionally apply one or multiple sampling strategies to the `WavefrontTracer`. See the [sampling documentation](https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp/blob/master/docs/sampling.md) for details.

#### Multi-valued Span Tags (Optional)
You can optionally add metadata to OpenTracing spans in the form of multi-valued tags. The `WavefrontTracer` builder supports different methods to add those tags.

```csharp
// Construct WavefrontTracer.Builder instance
WavefrontTracer.Builder wfTracerBuilder = new WavefrontTracer.Builder(...);

// Add individual tag key value
wfTracerBuilder.WithGlobalTag("env", "Staging");

// Add a dictionary of tags
wfTracerBuilder.WithGlobalTags(new Dictionary<string, string>{ { "severity", "sev-1" } });

// Add a dictionary of multivalued tags since Wavefront supports repeated tags
wfTracerBuilder.WithGlobalMultiValuedTags(new Dictionary<string, IEnumerable<string>>
{
    { "location", new string[]{ "SF", "NY", "LA" } }
});

// Construct Wavefront OpenTracing Tracer
ITracer tracer = wfTracerBuilder.Build();
```

#### Close the Tracer
Always close the tracer before exiting your application to flush all buffered spans to Wavefront.
```csharp
tracer.Close();
```

## Cross Process Context Propagation
See the [context propagation documentation](https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp/blob/master/docs/contextpropagation.md) for details on propagating span contexts across process boundaries.

## Application Metrics and Histograms
See the [application metrics documentation](https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp/blob/master/docs/metrics.md) for details on the out-of-the-box metrics and histograms that are provided.
