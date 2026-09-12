# TasmanianDevil

**Context-aware PII detection and de-identification for .NET.** A from-scratch engine whose
architecture is inspired by [Microsoft Presidio](https://github.com/microsoft/presidio) (MIT),
rebuilt as idiomatic, dependency-light C#. Framework-agnostic and fast.

```
dotnet add package TasmanianDevil
```

## Why TasmanianDevil

- **Validated, not just regex.** Recognizers carry real checksum validation (Luhn, IBAN mod-97,
  Verhoeff, ISO-7064, ICAO, bech32), so a 16-digit number is only a credit card if it actually checks out.
- **Context-aware scoring.** A bare token scores low and is dropped; nearby words ("card", "IBAN",
  "postcode") lift it over threshold via a dependency-free Porter-stemmer lemma matcher.
- **Reversible by design.** Encrypt PII, hand the opaque text to a third party, and decrypt the exact
  original back - the operator pipeline records enough to round-trip byte-for-byte.
- **Beyond plain text.** Structured JSON (by dotted path) and CSV (by inferred column) redaction, plus
  batch APIs over keyed records - all preserving shape and non-string values.
- **Offline core + an optional ML reach.** The whole engine runs with zero models. When you *want*
  more, the `TasmanianDevil.Onnx` add-on plugs a real multilingual span-NER model into the **same**
  pipeline (see below) - getting that working is the hard part, and TasmanianDevil ships it.

## Detection coverage

**Generic (always on):** CREDIT_CARD (Luhn), EMAIL_ADDRESS, IBAN_CODE (mod-97), CRYPTO (base58 +
bech32/bech32m), IP_ADDRESS (v4/v6), URL, MAC_ADDRESS, PHONE_NUMBER (libphonenumber).

**US pack (always on):** US_SSN, US_ITIN, ABA_ROUTING_NUMBER, US_BANK_NUMBER, US_DRIVER_LICENSE,
US_PASSPORT, US_NPI (Luhn), US_MBI, MEDICAL_LICENSE (DEA checksum).

**Opt-in country packs** (enabling all at once inflates false positives, so you choose): `uk`, `de`,
`in`, `it`, `es`, `nl` - each with validated national IDs, tax numbers, passports, driving licences, vehicle
registrations, etc.

## Quick start

```csharp
using TasmanianDevil;

var engine = new PiiEngine();
var result = engine.Deidentify("Email jane@contoso.com or call +1 425 555 0100.");
Console.WriteLine(result.AnonymizedText);
// Email <EMAIL_ADDRESS> or call <PHONE_NUMBER>.
```

### Operators

`replace` (default `<ENTITY_TYPE>`), `redact`, `mask`, `hash` (salted SHA-256/512 - the salt is
generated once per operator instance, so identical values hash identically; pass an explicit `salt`
to make digests reproducible across runs),
`encrypt`/`decrypt` (reversible AES-GCM), `keep`, and `custom` (your lambda):

```csharp
var options = new PiiOptions
{
    Operators = new Dictionary<string, OperatorConfig>
    {
        ["EMAIL_ADDRESS"] = new("mask", new() { [OperatorParams.CharsToMask] = 6 }),
        ["CREDIT_CARD"]   = new("redact"),
        ["DEFAULT"]       = new("encrypt", new() { [OperatorParams.Key] = key }),
    },
    Countries = [PiiCountries.De],
};
var engine = new PiiEngine(options);
var deid = engine.Deidentify(text);          // deid.IsReversible == true (all-encrypt)

var decrypt = new Dictionary<string, OperatorConfig>
{
    ["DEFAULT"] = new("decrypt", new() { [OperatorParams.Key] = key }),
};
var back = engine.Reidentify(deid, decrypt); // exact original, byte-for-byte
```

### Structured & batch

```csharp
engine.AnonymizeJson(json, new JsonRedactionScope { IncludePaths = ["user.email"] });
engine.AnonymizeCsv(header, rows);           // infers which columns are PII
engine.AnonymizeBatch(new Dictionary<string,string> { ["billing_email"] = "..." });
```

The lower-level engines (`AnalyzerEngine`, `AnonymizerEngine`, `DeanonymizerEngine`, `StructuredEngine`,
`Batch*Engine`) are all public if you want to compose them directly. See `samples/PiiShowcase` for a
narrated end-to-end tour.

## Optional multilingual ONNX NER

`TasmanianDevil.Onnx` adds **PERSON / LOCATION / ORGANIZATION / DATE_TIME** span detection - the entity
classes regex fundamentally cannot reach - via a zero-shot GLiNER model (mDeBERTa-v3 backbone),
multilingual out of the box. It registers as an ordinary recognizer, so its spans flow through the
exact same overlap-resolution and anonymization as the regex/checksum entities.

```
dotnet add package TasmanianDevil.Onnx
```

It runs the model through [Kyoto](https://github.com/filipw/kyoto). The ONNX export is published at
[`filip-w/gliner-multi-pii-onnx`](https://huggingface.co/filip-w/gliner-multi-pii-onnx) (fp16 default,
~580 MB):

```csharp
using TasmanianDevil.Onnx;

var ner = new GlinerNerRecognizer(new GlinerNerOptions
{
    ModelPath = modelPath, TokenizerPath = spmPath, ConfigPath = configPath,
});
registry.AddRecognizer(ner);   // now PERSON/LOCATION/... join the same analyzer pass
```

## Optional out-of-process detection (Remote / Azure)

Two add-ons let PII detection move **out of process** instead, while anonymization stays local - both
are detectors, not redactors: they return entity spans that flow through the same
`AnalyzerEngine`/`AnonymizerEngine` as every other recognizer.

`TasmanianDevil.Remote` speaks a generic HTTP contract - point it at any compatible service.
`TasmanianDevil.Azure` talks directly to the Azure AI Language REST API (no `Azure.AI.TextAnalytics`
SDK dependency), natively detecting `PERSON`, `ADDRESS`, `PHONE_NUMBER`, `EMAIL_ADDRESS`,
`ORGANIZATION`, `DATE_TIME`, `CREDIT_CARD`, `US_SSN`, `IP_ADDRESS`, `IBAN_CODE`, `URL` (see
`AzurePiiCategoryMap`) - configure `SupportedEntities`/`PiiCategories` for whichever subset you need.
`PERSON`/`ADDRESS` are the main reason to reach for it: free-form names and street addresses have no
checksum or fixed structure for the offline engine to validate.

> **Privacy note.** Both send the raw, unredacted analyzed text off-box. This is the inherent tradeoff
> of remote detection - only use it when you've accepted that, and prefer a network boundary you
> control over a public hop where possible.

```
dotnet add package TasmanianDevil.Remote   # generic HTTP contract - point at any compatible service
dotnet add package TasmanianDevil.Azure    # Azure AI Language - native Person + full street Address
```

The engine is **async-first**: `EntityRecognizer` has an `AnalyzeAsync` alongside sync `Analyze`
(defaulting to a zero-cost wrapper), and `AnalyzerEngine`/`PiiEngine` both expose `AnalyzeAsync`/
`AnonymizeAsync`/`DeidentifyAsync` counterparts. A remote recognizer is "just an async
`EntityRecognizer`" - it overrides `AnalyzeAsync`, leaves sync `Analyze` returning nothing, and the
sync API path silently ignores it.

```csharp
using TasmanianDevil.Remote;

var registry = PiiRecognizers.CreateRegistry("en");
registry.AddRecognizer(new RemotePiiRecognizer(
    new HttpPiiDetectionClient(new RemotePiiOptions { Endpoint = endpoint, SupportedEntities = [PiiEntities.Person] }),
    new RemotePiiOptions { Endpoint = endpoint, SupportedEntities = [PiiEntities.Person] }));

var engine = new PiiEngine(analyzer: new AnalyzerEngine(registry));
var result = await engine.AnonymizeAsync("Hi, this is John Smith.");
```

Or, for the Azure detector (native `Person`/`Address`, no `Azure.AI.TextAnalytics` SDK dependency):

```csharp
using TasmanianDevil.Azure;

var client = new AzurePiiClient(new AzurePiiOptions
{
    Endpoint = azureEndpoint,
    SubscriptionKey = azureKey,
    SupportedEntities = [PiiEntities.Person, PiiEntities.Address],
});
registry.AddRecognizer(new AzurePiiRecognizer(client, new AzurePiiOptions { /* same options */ }));
```

Both fail open by default (a remote failure yields no results for that request rather than throwing, so
local recognizers still redact what they can) and clamp/validate everything the remote side returns
(entity type must be among what was requested, score clamped to `[0,1]`, offsets must fit the analyzed
text) before trusting it. See each package's XML docs for the full option surface (timeout, auth,
category-map override, confidence threshold). The guardrail-level integration
(`.RedactPiiWithRemote()`/`.RedactPiiWithAzure()`) lives in [AgentGuard](https://github.com/filipw/AgentGuard)'s
`AgentGuard.RemotePii`/`AgentGuard.Azure` packages - see its `docs/remote-pii.md` for the full wire
contract, a sidecar recipe, and managed-identity setup.

## Attribution

See `THIRD_PARTY_NOTICES.txt` (Microsoft Presidio MIT, CommonRegex MIT, libphonenumber Apache-2.0,
public-domain Porter stemmer / Verhoeff / ISO-7064 / ICAO / Luhn algorithms).

## License

MIT
