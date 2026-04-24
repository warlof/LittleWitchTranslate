# Little Witch Translate

This tool can change the games assemblies to make Little Witch in the Woods support (a single) translation.

The translation within this repository is german but you can change the *.trans files to your liking.

The tool might not work out of the box when the game got an update as the code most likely will change and new texts are introduced to the game.

So please enjoy but with caution.

## Building yourself

To build this yourself you will have to resolve the reference to the games Assembly-CSharp.dll in the TranslatorPlugin project embedded in this repository.

You also need to add the ModManagerGUI project from my GitHub

## Available translation tables

| File                     | Language       |
| ------------------------ | -------------- |
| `data/table.trans`       | German (de-DE) |
| `data/table.fr-FR.trans` | French (fr-FR) |

To use a different translation, copy or rename the desired `*.trans` file to `table.trans` before running the tool.

## Automating translation

A python script is provided to ease start of translation using third party platform automation.

```bash
# Install the DeepL Python library
pip install deepl

# Set your API key (free tier works)
export DEEPL_API_KEY="your-key-here"

# Run — translates lines 2701 onwards, writes checkpoints every 500 lines
python translate.py
```

### Azure Translate

```bash
pip install install azure.ai.translation.text
export AZURE_API_KEY="your-key-here"
python translate.py --backend azure
```

### Google Translate

```bash
pip install google-cloud-translate
export GOOGLE_API_KEY="your-key-here"
python translate.py --backend google
```

Run `python translate.py --help` for all options.