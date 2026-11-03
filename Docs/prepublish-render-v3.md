# Producer current text/media contract (Source only)

Wire/capability: `prepublish-render/v3`, one current contract, no legacy fallback.
Existing route: `POST /public/v1/prepublish-render/occurrences/{id}/attach-rendered`.
Claim/render-token, correlation, timestamp, contentHash and Idempotency-Key unchanged.
Each sole target now REQUIRES `contentMode: "text_only" | "media"`; omission/unknown/null rejects.
SDK: `RenderTarget(IntegrationId, Channel, Caption, Media, ContentMode, Extras = null, PublishMode = null)`.
Constants: `RenderContentModes.TextOnly`, `.Media`, `.ContractVersion`.

`text_only`: exactly one target; resolved, non-whitespace caption; `media: []`;
no thumbnail field and no publishMode/story_sequence, including retained media presentation
in scheduled settings. Provider settings/media/content/length validation must accept the
final content before ReadyToPublish inside the existing serializable transaction.
`media`: one to ten final media, existing story sequence 2..10 and scope/MIME/project guards.
No fake image, renderer-failure conversion, media fallback, new endpoint or Temporal workflow.

Canonical digest stays sha256- + lower-case SHA256 over UTF8 canonical JSON of
{occurrenceId,correlation,targets,renderedAtUtc}; keys sorted ordinal, array order kept,
undefined/omitted fields omitted, explicit contentMode INCLUDED. Timestamp is the exact
wire string (no offset/fraction normalization by engine). JS JSON.stringify-compatible
string escaping is explicit in SDK canonicalization so Unicode/HTML characters agree.
Wire serializer is unchanged. HASH-GOLDENS-01.json holds shared Source literal vectors;
no SDK/Node hash test has been executed here.

Candidate Source versions SDK and engine: beta.8 -> 1.0.0-beta.9, breaking attach DTO.
No package/image/release publication or future immutable runtime pin is claimed.
Consumer/sealed/render Source03 stays exclusively with FinalizzazioneEditoriale;
consumer must supply explicit ContentMode and bind it in its approved/sealed digest.
Old v1/v2 capability claims are replaced by v3 on version discovery; route remains v1.