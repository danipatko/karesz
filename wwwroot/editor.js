require.config({ paths: { 'vs': 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs' } });

let _dotNetInstance;
const COMPILE_TIMEFRAME = 500; // ms

function addListeners(_editor) {
    // compile after x time without changes
    let timeoutID;
    const throttle = (func, timeFrame) => {
        clearTimeout(timeoutID);
        timeoutID = setTimeout(func, timeFrame);
    }

    // c# and monaco severity enum values differ
    const markerSeverityMap = {
        0: monaco.MarkerSeverity.Hint,
        1: monaco.MarkerSeverity.Info,
        2: monaco.MarkerSeverity.Warning,
        3: monaco.MarkerSeverity.Error,
    }

    const model = _editor.getModel()
    model.onDidChangeContent(() => {
        if (_dotNetInstance) {
            throttle(async () => {
                const markers = await _dotNetInstance.invokeMethodAsync("getDiagnostics", _editor.getValue())
                monaco.editor.setModelMarkers(model, "owner", markers.map(x => ({ ...x, severity: markerSeverityMap[x.severity] })));

            }, COMPILE_TIMEFRAME)
        }
    });

    // handle CTRL+S
    window.addEventListener('keydown', (e) => {
        if (e.ctrlKey && e.key == 's') {
            e.preventDefault();
            // TODO launch compile on save
        }
    });
}

function registerLangugageProvider(_editor) {
    // converts integer to enum
    const MonacoSymbolMap = {
        17: monaco.languages.CompletionItemKind.Array,
        16: monaco.languages.CompletionItemKind.Boolean,
        4: monaco.languages.CompletionItemKind.Class,
        13: monaco.languages.CompletionItemKind.Constant,
        8: monaco.languages.CompletionItemKind.Constructor,
        9: monaco.languages.CompletionItemKind.Enum,
        21: monaco.languages.CompletionItemKind.EnumMember,
        23: monaco.languages.CompletionItemKind.Event,
        7: monaco.languages.CompletionItemKind.Field,
        0: monaco.languages.CompletionItemKind.File,
        11: monaco.languages.CompletionItemKind.Function,
        10: monaco.languages.CompletionItemKind.Interface,
        19: monaco.languages.CompletionItemKind.Keyword,
        5: monaco.languages.CompletionItemKind.Method,
        1: monaco.languages.CompletionItemKind.Module,
        2: monaco.languages.CompletionItemKind.Namespace,
        20: monaco.languages.CompletionItemKind.Null,
        15: monaco.languages.CompletionItemKind.Number,
        18: monaco.languages.CompletionItemKind.Object,
        24: monaco.languages.CompletionItemKind.Operator,
        3: monaco.languages.CompletionItemKind.Package,
        6: monaco.languages.CompletionItemKind.Property,
        14: monaco.languages.CompletionItemKind.String,
        22: monaco.languages.CompletionItemKind.Struct,
        25: monaco.languages.CompletionItemKind.TypeParameter,
        12: monaco.languages.CompletionItemKind.Variable
    }

    // autocomplete
    monaco.languages.registerCompletionItemProvider('csharp', {
        provideCompletionItems: async (model, position) => {
            const offset = model.getOffsetAt(position);
            // console.log("current offset is " + offset);

            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn,
            };

            if (_dotNetInstance) {
                const data = await _dotNetInstance.invokeMethodAsync('getCompletionItems', _editor.getValue(), offset)

                return {
                    suggestions: data.map(({ asSnippet, ...x }) => {
                        return {
                            ...x,
                            range,
                            kind: MonacoSymbolMap[x.kind],
                            // move cursor if asSnippet is true
                            ...(asSnippet ? { insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet } : {})
                        }
                    })
                };
            }

            return { suggestions: [] };
        },
    });

    // hover information
    monaco.languages.registerHoverProvider('csharp', {
        provideHover: async (model, position) => {
            const offset = model.getOffsetAt(position);
            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn,
            };

            if (_dotNetInstance) {
                const data = await _dotNetInstance.invokeMethodAsync('getHoverinfo', _editor.getValue(), offset);
                if (data != null) {
                    return { range, contents: [{ value: `\`\`\`csharp\n${data}\n\`\`\`` }] }
                }
            }
        }
    });
}

// this object is accessible by the .NET JSRuntime
window.Editor = window.Editor || (function () {
    let _editor;

    return {
        create: function (id, value, dotNetInstance) {
            _dotNetInstance = dotNetInstance;

            require(['vs/editor/editor.main'], () => {
                monaco.languages.register({ id: 'csharp' });
                // create options
                // https://microsoft.github.io/monaco-editor/typedoc/interfaces/editor.IStandaloneEditorConstructionOptions.html
                _editor = monaco.editor.create(document.getElementById(id), {
                    value: value || '',
                    language: 'csharp',
                    theme: 'vs-dark',
                    inlineSuggest: { enabled: true },
                    codeLens: true,
                    quickSuggestions: false,
                    cursorSmoothCaretAnimation: 'explicit',
                    automaticLayout: true,
                    mouseWheelZoom: true,
                    bracketPairColorization: { enabled: true },
                    minimap: { enabled: true }
                });

                registerLangugageProvider(_editor);
                addListeners(_editor);
            });
        },
        getValue: function () {
            return _editor.getValue();
        },
        setValue: function (value) {
            if (_editor) {
                _editor.setValue(value);
            } else {
                _overrideValue = value;
            }
        },
        focus: function () {
            return _editor.focus();
        },
        setTheme: function (theme) {
            monaco.editor.setTheme(theme);
        },
        dispose: function () {
            _editor = null;
        },
        // TODO: not here
        scrollLogs: function (id) {
            const element = document.getElementById(id);
            if (!!element) element.scrollTop = element.scrollHeight;
        },
        copyToClipboard: function (level) {
            const code = _editor.getValue();
            const text = window.location.origin + "/?" + new URLSearchParams({ code, level }).toString()

            navigator.clipboard.writeText(text)
                .then(() => {
                    alert("copied!");
                }, (err) => {
                    console.error('Could not copy text: ', err);
                });

        },
    }
}());

//window.onbeforeunload = (e) => {
//    e.preventDefault();
//    return `Leave site? Changes that you made may not be saved.`;
//}
