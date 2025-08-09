## Basic Pipeline Creation

### Multi-stage tweet processing pipeline:

```csharp
var pipeline = new Pipeline&lt;Tweet&gt;();

// Content moderation stage
pipeline.Add(Pipe&lt;Tweet&gt;
    .Action(tweet =&gt; {
        if (IsSpam(tweet) || HasProfanity(tweet))
            tweet.Status = TweetStatus.Blocked;
    })
    .Id("content-moderator")
    .DegreeOfParallelism(4)
    .ToPipe());

// Enrichment stage (only for clean tweets)
pipeline.Add(Pipe&lt;Tweet&gt;
    .Action(async tweet =&gt; {
        tweet.Sentiment = await AnalyzeSentiment(tweet.Text);
        tweet.Entities = ExtractEntities(tweet.Text);
    })
    .Id("enricher")
    .Filter(tweet =&gt; tweet.Status != TweetStatus.Blocked)
    .DegreeOfParallelism(8)
    .ToPipe());

// Batch for analytics storage
pipeline.Add(Pipe&lt;Tweet&gt;
    .Batch(1000, async tweets =&gt; {
        await BigQueryClient.InsertRows("tweets_analytics", tweets);
    })
    .Id("analytics-writer")
    .BatchTriggerPeriod(TimeSpan.FromSeconds(10))
    .ToPipe());

// Process tweets
foreach (var tweet in tweetStream)
    await pipeline.Send(tweet);

await pipeline.Complete();
```

## Error Handling and Routing

Pipeline with sentiment-based routing:

```csharp
var positivePipe = Pipe&lt;Tweet&gt;
    .Action(async tweet =&gt; {
        await NotifyMarketingTeam(tweet);
        await AddToSuccessStories(tweet);
    })
    .Id("positive-handler")
    .ToPipe();

var negativePipe = Pipe&lt;Tweet&gt;
    .Action(async tweet =&gt; {
        await NotifySupportTeam(tweet);
        await CreateSupportTicket(tweet);
    })
    .Id("negative-handler")
    .ToPipe();

var sentimentPipe = Pipe&lt;Tweet&gt;
    .Action(async tweet =&gt; {
        tweet.Sentiment = await AnalyzeSentiment(tweet.Text);
    })
    .Id("sentiment-analyzer")
    .Route(tweet =&gt; {
        return tweet.Sentiment switch {
            Sentiment.Positive =&gt; positivePipe,
            Sentiment.Negative =&gt; negativePipe,
            _ =&gt; null // Neutral tweets continue to next pipe
        };
    })
    .ToPipe();
```

## Fork-Join Parallel Processing

Fork-join pipeline for parallel tweet enrichment:

```csharp
var sentimentBlock = Parallel&lt;Tweet&gt;
    .Action(async tweet =&gt; {
        tweet.Sentiment = await AnalyzeSentiment(tweet.Text);
    })
    .Id("sentiment")
    .DegreeOfParallelism(3);

var languageBlock = Parallel&lt;Tweet&gt;
    .Action(async tweet =&gt; {
        tweet.Language = await DetectLanguage(tweet.Text);
    })
    .Id("language")
    .DegreeOfParallelism(2);

var entitiesBlock = Parallel&lt;Tweet&gt;
    .Action(tweet =&gt; {
        tweet.Entities = ExtractEntities(tweet.Text);
    })
    .Id("entities");

var viralCheckBlock = Parallel&lt;Tweet&gt;
    .Action(tweet =&gt; {
        tweet.IsViral = (tweet.RetweetCount > 1000 || tweet.LikeCount > 5000);
    })
    .Id("viral-check");

var forkPipe = Pipe&lt;Tweet&gt;
    .Fork(sentimentBlock, languageBlock, entitiesBlock, viralCheckBlock)
    .Join(tweet =&gt; Console.WriteLine($"Enriched tweet {tweet.Id}"))
    .Id("enrichment-fork")
    .ToPipe();

// All four enrichments run in parallel for each tweet
await forkPipe.Send(tweet);

forkPipe.Complete();
await forkPipe.Completion;
```