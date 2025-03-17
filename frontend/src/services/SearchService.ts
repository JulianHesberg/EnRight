import axios from "axios";
import {FileToGet} from "../models/FileToGet.ts";

const API_BASE_URL = 'http://localhost:5000/api/File/search';

export const search = async (searchQuery: string): Promise<FileToGet[]> => {
    try {
        const response = await axios.get(API_BASE_URL, {
            params: { searchQuery: searchQuery }
        });
        return response.data as FileToGet[];
    } catch (error) {
        console.error('Error fetching search results:', error);
        throw error;
    }
};
