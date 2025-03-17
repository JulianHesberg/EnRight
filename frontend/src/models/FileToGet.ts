export interface FileToGet {
    fileId: number;
    fileName: string;
    content: Uint8Array;
    occurrenceSum: number;
}