# Sampling

In order to reduce the volume of traces that your application sends to Wavefront, you can apply a sampling strategy to the `WavefrontTracer`. You can do so by adding an instance of the `ISampler` interface to the `WavefrontTracer` builder.

For instance, if you want to report approximately 1 out of every 5 traces, you can use a `RateSampler`:

```csharp
WavefrontTracer.Builder wfTracerBuilder = ...  // instantiate your WavefrontTracer builder

wfTracerBuilder.WithSampling(new RateSampler(0.2));

// Optionally configure your WavefrontTracer builder further before building
ITracer tracer = wfTracerBuilder.Build();
```

If you want to apply multiple sampling strategies, you can use a `CompositeSampler`. A `CompositeSampler` delegates the sampling decision to multiple other samplers and decides to allow a span if any of the delegate samplers decide to allow it. For instance, if you want to report approximately 10% of traces but you also don't want to lose any spans that are over 60 seconds long, you can do the following:

```csharp
WavefrontTracer.Builder wfTracerBuilder = ...  // instantiate your WavefrontTracer builder

ISampler rateSampler = new RateSampler(0.1);
ISampler durationSampler = new DurationSampler(TimeSpan.FromSeconds(60_000));
ISampler compositeSampler = new CompositeSampler(new List<ISampler>{ rateSampler, durationSampler });
wfTracerBuilder.WithSampling(compositeSampler));

// Optionally configure your WavefrontTracer builder further before building
ITracer tracer = wfTracerBuilder.Build();
```

This library provides the following ISampler implementations:

| ISampler              | Description                            |
| --------------------- | -------------------------------------- |
| ConstantSampler       | Allows either all traces or no traces. Specify `true` during instantiation to sample all traces, or `false` to sample no traces. |
| DurationSampler       | Allows a span if its duration exceeds a specified threshold. Specify the duration threshold in milliseconds during instantiation. |
| RateSampler           | Allows a specified probabilistic rate of traces to be reported. Specify the rate (between 0.0 and 1.0) of traces to allow during instantiation. |
| CompositeSampler      | Delegates the sampling decision to multiple other samplers and allows a span if any delegate decides to allows it. Specify a list of samplers to delegate to during instantiation. |

Do note that regardless of the sampling strategy that is employed, the `WavefrontTracer` will always sample error spans (with `error=true` span tag) and spans that have a sampling priority (`sampling.priority` span tag) greater than 0.

Also note that regardless of sampling, [application metrics and histograms](https://github.com/wavefrontHQ/wavefront-opentracing-sdk-csharp/blob/master/README.md#application-metrics-and-histograms) will automatically be collected and reported for all spans created by the `WavefrontTracer`.