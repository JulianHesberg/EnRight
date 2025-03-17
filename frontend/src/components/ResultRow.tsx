import {FileToGet} from "../models/FileToGet.ts";

interface ResultRowProps {
    file: FileToGet
}

function ResultRow({ file }: ResultRowProps) {

    const handleDownload = () => {
    const raw = atob(file.content.toString());

    // Convert that binary string to a typed array
    const array = new Uint8Array([...raw].map(c => c.charCodeAt(0)));

    // Decode as text
    const textContent = new TextDecoder("utf-8").decode(array);

    // Create a blob.
    const blob = new Blob([textContent], { type: "text/plain" });
        // Create an object URL
        const url = URL.createObjectURL(blob);

        // Programmatically click a temporary <a> link
        const link = document.createElement("a");
        link.href = url;

        link.download = file.fileName.endsWith(".txt")
            ? file.fileName
            : file.fileName + ".txt";

        document.body.appendChild(link);
        link.click();

        // Cleanup
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    };
    return (
        <tr>
            <td>{file.fileName}</td>
            <td>
                <button onClick={handleDownload}>
                    Download File
                </button>
            </td>
        </tr>
    )
}

export default ResultRow;