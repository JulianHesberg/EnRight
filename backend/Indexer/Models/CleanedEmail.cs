﻿namespace Indexer.Models;

public class CleanedEmail
{
    public string FileName { get; set; }
    public string Content { get; set; }
    public byte[] Data { get; set; }
}