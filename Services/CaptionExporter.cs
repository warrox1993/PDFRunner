using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ConvertPDF.Models;

namespace ConvertPDF.Services
{
    public static class CaptionExporter
    {
        /// <summary>
        /// Exports transcription segments to SRT format.
        /// </summary>
        public static string ExportToSrt(TranscriptionResult result)
        {
            var sb = new StringBuilder();
            int index = 1;

            foreach (var segment in result.Segments)
            {
                sb.AppendLine(index.ToString());
                sb.AppendLine($"{FormatTimeSrt(segment.Start)} --> {FormatTimeSrt(segment.End)}");
                sb.AppendLine(segment.Text.Trim());
                sb.AppendLine();
                index++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports transcription segments to WebVTT format.
        /// </summary>
        public static string ExportToVtt(TranscriptionResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("WEBVTT");
            sb.AppendLine();

            foreach (var segment in result.Segments)
            {
                sb.AppendLine($"{FormatTimeVtt(segment.Start)} --> {FormatTimeVtt(segment.End)}");
                sb.AppendLine(segment.Text.Trim());
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats transcription with timestamps for display.
        /// </summary>
        public static string FormatWithTimestamps(TranscriptionResult result)
        {
            var sb = new StringBuilder();
            foreach (var segment in result.Segments)
            {
                sb.AppendLine($"[{segment.Start:mm\\:ss}] {segment.Text.Trim()}");
            }
            return sb.ToString();
        }

        private static string FormatTimeSrt(TimeSpan time)
        {
            return $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
        }

        private static string FormatTimeVtt(TimeSpan time)
        {
            return $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
        }

        public static void SaveToFile(string content, string filePath)
        {
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
    }
}
