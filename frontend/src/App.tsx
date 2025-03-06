import './App.css'
import SearchBar from "./components/SearchBar.tsx";
import {FileToGet} from "./models/FileToGet.ts";
import ResultTable from "./components/ResultTable.tsx";
import {useState} from "react";
import {search} from "./services/SearchService.ts";

function App() {
    const [files, setFiles] = useState<FileToGet[]>([]);

    const handleFilesSearch = (query: string) => {
        console.log(query);
        const result = search(query);
        setFiles(result);
    }

  return (
    <>
        <div>
            <SearchBar handleSearch={handleFilesSearch}></SearchBar>
            <ResultTable results={files}></ResultTable>
        </div>

    </>
  )
}

export default App
