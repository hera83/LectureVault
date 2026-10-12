# AiGateway (service)

**Formål:** Klient til den interne AiGateway, som samler Ollama (LLM), Speaches (tale/lyd), en knowledge base (RAG) og administration af gatewayens egne API-nøgler bag ét HTTP-API.
**Brug det når:** opgaven skal bruge AI gennem gatewayen: chat, generering, embeddings, tale-til-tekst, tekst-til-tale, dokumentsøgning eller RAG-chat.
**Brug det ikke til:** direkte kald til en Ollama-server uden gatewayen (brug `Ollama`-servicen).
**Eksternt system og konfiguration:** AiGateway-API'et, spec i `Documents/swagger.json`. Sektionen `AiGateway` i `appsettings.json`: `BaseUrl`, `ApiKey` (secret, sendes som `X-Api-Key`), `RequestTimeoutSeconds` og standardværdierne `DefaultChatModel`, `DefaultLanguage`, `DefaultTtsModel`, `DefaultTtsVoice` og `DefaultSttModel`. Full pattern.

## Metoder
| Gruppe (`Dtos/<Gruppe>/`) | Metoder | Beskrivelse |
|---|---|---|
| Health | `CheckHealthAsync` | Gatewayens og de underliggende systemers helbred. |
| Keys | `CreateKeyAsync`, `ListKeysAsync`, `GetKeyByIdAsync`, `UpdateKeyAsync`, `RolloverKeyAsync`, `ActivateKeyAsync`, `DeactivateKeyAsync`, `DeleteKeyAsync`, `GetKeyAuditLogAsync` | Administration af gatewayens egne API-nøgler. Kræver en admin-nøgle i gatewayen (ellers 403). `GetKeyAuditLogAsync` returnerer `List<AuditLogEntryResponseDto>`. |
| KnowledgeBase | `CreateGroupAsync`, `ListGroupsAsync`, `DeleteGroupAsync`, `UploadDocumentAsync`, `ListDocumentsAsync`, `DeleteDocumentAsync`, `DownloadDocumentAsync`, `SearchKnowledgeBaseAsync`, `RagChatAsync` | Dokumentgrupper, upload/download, semantisk søgning og chat med svar ud fra dokumenterne. `ListGroupsAsync`, `ListDocumentsAsync` og `SearchKnowledgeBaseAsync` returnerer `List<…>` direkte. Kræver KB-rettighed i gatewayen: den almindelige nøgle har den, admin-nøglen har den ikke (403). |
| Logs | `SearchLogsAsync` | Søgning i gatewayens egne logs. Kræver en admin-nøgle i gatewayen (ellers 403). |
| Ollama | `OllamaGenerateAsync`, `OllamaGenerateStreamAsync`, `OllamaChatAsync`, `OllamaChatStreamAsync`, `OllamaEmbedAsync`, `OllamaListModelsAsync`, `OllamaListRunningModelsAsync`, `OllamaShowModelAsync`, `OllamaCopyModelAsync`, `OllamaDeleteModelAsync`, `OllamaPullModelAsync`, `OllamaPushModelAsync`, `OllamaCreateModelAsync`, `OllamaGetVersionAsync` | Ollama gennem gatewayen. `…StreamAsync` returnerer `IAsyncEnumerable`. |
| Speaches | `SpeachesChatCompletionsAsync`, `SpeachesTranscribeAsync`, `SpeachesTranscribeRealtimeAsync`, `SpeachesTranslateAsync`, `SpeachesListModelsAsync`, `SpeachesListAudioModelsAsync`, `SpeachesListVoicesAsync`, `SpeachesGetModelAsync`, `SpeachesDownloadModelAsync`, `SpeachesDeleteModelAsync`, `SpeachesGetRegistryAsync`, `SpeachesListRunningModelsAsync`, `SpeachesLoadModelAsync`, `SpeachesStopModelAsync`, `SpeachesSynthesizeSpeechAsync`, `SpeachesCreateSpeechEmbeddingAsync`, `SpeachesDetectSpeechTimestampsAsync`, `SpeachesDiarizeAsync` | Tale-til-tekst, oversættelse, tekst-til-tale, speaker-embeddings, taledetektion, diarisering og modelstyring. |

## Faldgruber
- Alle fejl kastes som `AiGatewayException`. `StatusCode` er den status, appen svarer med, og `UpstreamStatusCode` er gatewayens egen. Controllerne fanger den og viser `Message` som toast eller `{ error }`-chunk (se projektets afvigelser i `.claude/CLAUDE.md`). Fanges den ikke, giver `GlobalExceptionHandler` fejlsiden eller ProblemDetails. Oversættelsen sker i `EnsureSuccessAsync`/`SendAsync` i `AiGatewayService.cs`:
  - Gatewayen svarer 401 (nøglen i `AiGateway:ApiKey` mangler, er ugyldig eller udløbet) → **502**. Det er en fejl i serverens opsætning, ikke i brugerens login.
  - Gatewayen svarer 403 (nøglen mangler rettighed, fx admin til `Keys`/`Logs`) → **403** med en dansk besked, der nævner endpointet.
  - Gatewayen kan ikke nås → **502**. Timeout (`AiGateway:RequestTimeoutSeconds`) → **504**.
  - Svaret kan ikke læses som DTO'en (fx fordi gatewayen har ændret format) → **502** med endpoint og typenavn.
  - Alle andre statuskoder sendes uændret videre med gatewayens besked.
- Den almindelige nøgle har ikke admin-rettighed, så `Keys`- og `Logs`-metoderne giver 403 med den. Byg ikke funktioner til den primære bruger oven på dem.
- `SpeachesTranscribeRealtimeAsync` er midlertidigt deaktiveret og kaster altid `NotSupportedException` på grund af en fejl i Speaches. Se kommentaren i `AiGatewayService.Speaches.cs`.
- `DownloadDocumentAsync` og `SpeachesSynthesizeSpeechAsync` returnerer `AiGatewayFileResultDto` (`Dtos/Common/`) med en åben `Stream`. Kalderen skal disponere den. Skal filen gemmes, gælder `files.md`.
- Gruppen `Ollama` og gruppen `Speaches` har begge en `ListModelsResponseDto`. Brug et alias i `using`, når begge skal bruges i samme fil (som i `IAiGatewayService`).
- Standardværdierne (`DefaultChatModel` osv.) læses ind i `AiGatewaySettings`, men servicen udfylder dem ikke automatisk i requests.
- Gatewayens spec beskriver ikke 200-svaret for flere endpoints (fx `Keys/GetById`, `Rollover`, `GetAuditLog`, `KnowledgeBase/CreateGroup`). DTO'erne er i stedet testet mod den kørende gateway (12-10-2026).
- `SpeachesLoadModelAsync` hang i 600 sekunder og tog Speaches ned under testen (12-10-2026). Brug den med forsigtighed.
- Fejl i Speaches selv (12-10-2026): `SpeachesChatCompletionsAsync` giver 500, og `SpeachesCreateSpeechEmbeddingAsync` og `SpeachesDiarizeAsync` giver 404, fordi Speaches-versionen bag gatewayen ikke har de endpoints.
