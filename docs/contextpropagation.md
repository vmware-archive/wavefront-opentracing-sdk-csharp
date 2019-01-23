# Cross Process Context Propagation

Following the [OpenTracing standard](https://opentracing.io/docs/overview/inject-extract/), you must arrange for your application's `WavefrontTracer` to propagate a span context across process boundaries whenever a client microservice sends a request to another microservice. Doing so enables you to represent the client's request as part of a continuing trace that consists of multiple connected spans. 

The `WavefrontTracer` provides `Inject` and `Extract` methods that can be used to propagate span contexts across process boundaries. You can use these methods to propagate `ChildOf` or `FollowsFrom` relationship between spans across process or host boundaries.

* In code that makes an external call (such as an HTTP invocation), obtain the current span and its span context, create a carrier, and inject the span context into the carrier:

```csharp
currentSpan = ...  // obtain the current span
ITextMap carrier = new TextMapInjectAdapter(new Dictionary<string, string>());
tracer.Inject(currentSpan.Context, BuiltinFormats.HttpHeaders, carrier);

// loop over the injected text map and set its contents on the HTTP request header...
```

* In code that responds to the call (i.e., that receives the HTTP request), extract the propagated span context:

```csharp
ITextMap carrier = new TextMapExtractAdapter(new Dictionary<string, string>());
ISpanContext ctx = tracer.Extract(BuiltinFormats.HttpHeaders, carrier);
IScope receivingScope = tracer.BuildSpan("httpRequestOperationName").AsChildOf(ctx).StartActive(true);
```
