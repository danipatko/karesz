require.config({ paths: { 'vs': 'lib/monaco-editor/min/vs' } });

let _dotNetInstance;
const throttleLastTimeFuncNameMappings = {};

function registerLangugageProvider(_editor) {
    monaco.languages.registerCompletionItemProvider('csharp', {
        provideCompletionItems: async function (model, position) {
            const offset = model.getOffsetAt(position);
            console.log("current offset is " + offset);
            console.log(_dotNetInstance);

            _dotNetInstance && _dotNetInstance?.invokeMethodAsync('getCompletionItems', _editor.getValue(), offset);

            return {
                suggestions: [],
            };
        },
    });
}

//function onKeyDown(e) {
//    if (e.ctrlKey && e.key == 's') {
//        e.preventDefault();

//        if (_dotNetInstance && _dotNetInstance.invokeMethodAsync) {
//            throttle(() => _dotNetInstance.invokeMethodAsync('TriggerCompileAsync'), 1000, 'compile');
//        }
//    }
//}

function throttle(func, timeFrame, id) {
    const now = new Date();
    if (now - throttleLastTimeFuncNameMappings[id] >= timeFrame) {
        func();
        throttleLastTimeFuncNameMappings[id] = now;
    }
}

window.Editor = window.Editor || (function () {
    let _editor;

    return {
        create: function (id, value, dotNetInstance) {
            if (!id) { return; }

            _dotNetInstance = dotNetInstance;
            throttleLastTimeFuncNameMappings['compile'] = new Date();

            require(['vs/editor/editor.main'], () => {
                _editor = monaco.editor.create(document.getElementById(id), {
                    value: value || '',
                    language: 'csharp',
                    theme: 'vs-dark',
                    inlineSuggest: { enabled: true },
                    codeLens: true,
                    cursorSmoothCaretAnimation: 'explicit',
                    automaticLayout: false,
                    mouseWheelZoom: true,
                    bracketPairColorization: { enabled: true },
                    minimap: { enabled: true }
                });

                registerLangugageProvider(_editor);
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
        }
    }
}());

