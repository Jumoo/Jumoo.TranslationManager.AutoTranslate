## What does this change?

<!-- A sentence or two on the change and why it is needed. -->

## Notes for the reviewer

<!-- Anything non-obvious: behaviour changes, things you decided against, areas you want a
     second opinion on. Delete if there is nothing to say. -->

## Checklist

- [ ] `dotnet build Jumoo.TranslationManager.AutoTranslate/Jumoo.TranslationManager.AutoTranslate.csproj -c Release` is clean
- [ ] Changes were exercised against a running site (e.g. `AutoApprove.Site17`), not just built -
      this sample runs on content save/publish events, which aren't covered by a plain build
- [ ] If a dependency version changed, the target framework(s) it applies to still build
