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
`in`, `it`, `es` - each with validated national IDs, tax numbers, passports, driving licences, vehicle
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

`replace` (default `<ENTITY_TYPE>`), `redact`, `mask`, `hash` (salted SHA-256/512),
`encrypt`/`decrypt` (reversible AES-CBC), `keep`, and `custom` (your lambda):

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

## Attribution

See `THIRD_PARTY_NOTICES.txt` (Microsoft Presidio MIT, CommonRegex MIT, libphonenumber Apache-2.0,
public-domain Porter stemmer / Verhoeff / ISO-7064 / ICAO / Luhn algorithms).

## License

MIT
