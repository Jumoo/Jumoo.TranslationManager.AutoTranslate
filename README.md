# Jumoo.TranslationManager.AutoTranslate

Sample code/package that will push things through translation
on content save/publish.

> [!WARNING]
> This is sample/beta code, do check rigously before use - it would be possible for this code to run away and just carry on translating everytime something is published so you should check that isn't something that would cause you financial harm.

Shows how to create nodes and jobs upon events, and then push them through the stages using the core services.

this is off by default requires a bit of config on the site.

```json
  "Translation": {
    "Auto": {
      "OnPublish": true,
      "Provider": "F9CD9683-5F0A-4407-9E5D-BD7295FEFEB1"
    }
  }
```

# OnPublish

Perform the automatic translation when things are published (prefered.)

# OnSave

Perform the automatic translation when things are saved

# Provider

The provider value is the 'key' of the provider you wish to use, you can get this from the url on the provider page or below
for the well known ones.

| Provider                  | Key                                    | Packages                                 |
| ------------------------- | -------------------------------------- | ---------------------------------------- |
| Microsoft Translation API | "F9CD9683-5F0A-4407-9E5D-BD7295FEFEB1" | Core                                     |
| Google Translation Api    | 4C26E0B5-0D73-40A2-9EDE-30884BC3C94F   | Core                                     |
| DeepL Client              | B5F17527-3CC6-4C49-A95E-E852541B4167   | Jumoo.TranslationManager.Connector.DeepL |
| OpenAI Client             | D60C3B07-2FCE-4568-8A1D-C3286A73DF8A   | Jumoo.TranslationManager.OpenAi          |

# ExludeSets

Ids (int values) of the sets you don't want to do this for.

# ExludeCultures

The cultures you don't want to do this for.
