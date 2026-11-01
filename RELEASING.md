# Template release versions

New template tags and `template.version` use the same full version as the SDK package, such as `0.2.10-26w44a`. `template.release.json` records the selected game commit and SDK commit for traceability. For the initial conversion of the existing 26w43a template, the original game commit is unknown and is recorded as null; its SDK commit is verified from the published package.

Run the Redux game's `scripts/new-sdk-snapshot.ps1 -TemplateRepository <clean-clone> -SourceRef origin/<source> -Prepare`. The helper automatically chooses the current UTC ISO week and next unused letter after checking SDK and Redux.Templates tags. Pass `-Snapshot <weekly-label>` only to override that choice. It derives the game version from the captured source commit rather than a fixed prefix, switches the SDK dependency to the public scoped registry, and prepares the version stamp and provenance file. Reuse the captured source commit and label for publication verification. Follow the game's `sdk-snapshot` procedure for its Unity/package checks and Git publication steps.

The registry must contain the exact SDK version and source commit before publishing the template tag. Keep the template tag lightweight and publish the main branch before the tag. Older tags stay unchanged, and the SDK Manager continues supporting projects stamped with them.
