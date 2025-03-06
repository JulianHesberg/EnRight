import {FileToGet} from "../models/FileToGet.ts";

const data: FileToGet[] = [
    {fileName: "Big Tits", fileId: 1},
    {fileName: "Small Tits", fileId: 2},
    {fileName: "Big Dick", fileId: 3},
    {fileName: "Small Dick", fileId: 4}
];

export const search = (query: string): FileToGet[] => {
    const result = data.filter((fileToGet: FileToGet) => fileToGet.fileName.includes(query));
    console.log(result);
    return result;
};
