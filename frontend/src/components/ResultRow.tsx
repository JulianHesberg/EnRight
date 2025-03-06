import {FileToGet} from "../models/FileToGet.ts";

interface ResultRowProps {
    file: FileToGet
}

function ResultRow({ file }: ResultRowProps) {

    const handleDownload = ()=> {
        console.log(file.fileId)
    }

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