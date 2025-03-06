import {FileToGet} from "../models/FileToGet.ts";
import ResultRow from "./ResultRow.tsx";

interface ResultTableProps {
    results: FileToGet[]
}

function ResultTable({results}: ResultTableProps) {

    return (
        <table>
            <thead>
                <tr>
                    <th>File Name</th>
                    <th>Download</th>
                </tr>
            </thead>
            <tbody>
            {results.map((result) => (
                <ResultRow key={result.fileId} file={result}></ResultRow>
            ))}
            </tbody>
        </table>
    );
}
export default ResultTable;