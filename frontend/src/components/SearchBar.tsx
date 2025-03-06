import {useState} from "react";
import * as React from "react";


interface SearchBarProps {
    handleSearch: (query: string) => void;
}

function SearchBar(props: SearchBarProps) {
    const [searchTerm, setSearchTerm] = useState('');

    const handleSearchTermChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setSearchTerm(e.target.value)
    }

    const handleClickSearch = () => {
        props.handleSearch(searchTerm);
    }

    return (
        <>
            <div className="search-bar">
                <input onChange={handleSearchTermChange} value={searchTerm} placeholder="Search..." />
                <button onClick={handleClickSearch}>
                    SEARCH!
                </button>
            </div>
        </>
    );
}

export default SearchBar;