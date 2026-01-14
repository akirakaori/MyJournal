let quill;
let dotNetRef;

window.initQuill = (divId, dotNetReference) => {
    dotNetRef = dotNetReference;

    // Initialize Quill with enhanced options
    quill = new Quill(`#${divId}`, {
        theme: "snow",
        placeholder: "Start writing…",
        modules: {
            toolbar: [
                [{ 'header': [1, 2, 3, false] }],
                ['bold', 'italic', 'underline', 'strike'],
                [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                [{ 'indent': '-1' }, { 'indent': '+1' }],
                ['link', 'image'],
                ['clean']
            ]
        }
    });

    // Listen for text changes and notify Blazor
    quill.on('text-change', function () {
        const html = quill.root.innerHTML;
        const length = quill.getLength() - 1; // Subtract 1 for trailing newline

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnQuillContentChanged', html, length);
        }
    });
}

window.getQuillHtml = () => {
    return quill ? quill.root.innerHTML : '';
}

window.setQuillHtml = (htmlContent) => {
    if (quill) {
        quill.root.innerHTML = htmlContent || '';
    }
}