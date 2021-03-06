﻿using MediaToolkit.Model;
using MediaToolkit.Options;
using MediaToolkit.Util;
using System;
using System.Globalization;
using System.Text;

namespace MediaToolkit
{
    internal class CommandBuilder
    {
        internal static string Serialize(EngineParameters engineParameters)
        {
            switch (engineParameters.Task)
            {
                case FFmpegTask.Convert:
                    return Convert(engineParameters.InputFile, engineParameters.OutputFile, engineParameters.ConversionOptions);

                case FFmpegTask.GetMetaData:
                    return GetMetadata(engineParameters.InputFile);

                case FFmpegTask.GetThumbnail:
                    return GetThumbnail(engineParameters.InputFile, engineParameters.OutputFile, engineParameters.ConversionOptions);
                case FFmpegTask.Concatenate:
                    return Concatenate(engineParameters);
            }
            return null;
        }

        private static string Concatenate(EngineParameters engineParameters)
        {
            bool sameCodec = true;
            var lastformat = "";
            foreach(MediaFile mf in engineParameters.ConcatFiles)
            {
                if(!String.IsNullOrEmpty(lastformat)&& lastformat!= mf.Metadata.VideoData.Format)
                {
                    sameCodec = false;
                    break;
                }
                lastformat=mf.Metadata.VideoData.Format;
            }
            //sameCodec = false;
            return sameCodec ? ConcatenateDemux(engineParameters) : ConcatenateFilter(engineParameters);
        }
        private static string ConcatenateDemux(EngineParameters engineParameters)
        {
            var commandBuilder = new StringBuilder();
            var outputFile = engineParameters.OutputFile;
            string filelist = "";
            foreach (MediaFile mf in engineParameters.ConcatFiles)
            {
                filelist += "file '" + mf.Filename + "'" + Environment.NewLine;
            }
            string listfilename = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllText(listfilename, filelist);
            commandBuilder.AppendFormat("-f concat -i \"{0}\" -c copy ", listfilename);
            return commandBuilder.AppendFormat(" \"{0}\" ", outputFile.Filename).ToString();
        }
        private static string ConcatenateFilter(EngineParameters engineParameters)
        {
            var commandBuilder = new StringBuilder();
            var outputFile = engineParameters.OutputFile;
            string filelist = "";
            string filter = "-filter_complex \"";
            int vidx = 0;
            foreach (MediaFile mf in engineParameters.ConcatFiles)
            {
                filelist += "-i \"" + mf.Filename + "\" " ;
                filter += string.Format("[{0}:v:0] [{0}:a:0] ", vidx);
                vidx++;
            }
            filter += string.Format("concat=n={0}:v=1:a=1 [v] [a]\"", vidx);
            commandBuilder.AppendFormat("{0} {1} -map \"[v]\" -map \"[a]\" ", filelist,filter);
            return commandBuilder.AppendFormat(" \"{0}\" ", outputFile.Filename).ToString();
        }
        private static string GetMetadata(MediaFile inputFile)
        {
            return string.Format("-i \"{0}\" ", inputFile.Filename);
        }

        private static string GetThumbnail(MediaFile inputFile, MediaFile outputFile, ConversionOptions conversionOptions)
        {
            var commandBuilder = new StringBuilder();

            commandBuilder.AppendFormat(CultureInfo.InvariantCulture, " -ss {0} ",
                conversionOptions.Seek.GetValueOrDefault(TimeSpan.FromSeconds(1)).TotalSeconds);

            commandBuilder.AppendFormat(" -i \"{0}\" ", inputFile.Filename);
            commandBuilder.AppendFormat(" -vframes {0} ", 1);

            return commandBuilder.AppendFormat(" \"{0}\" ", outputFile.Filename).ToString();
        }

        private static string Convert(MediaFile inputFile, MediaFile outputFile, ConversionOptions conversionOptions)
        {
            var commandBuilder = new StringBuilder();

            // Default conversion
            if (conversionOptions == null)
                return commandBuilder.AppendFormat(" -i \"{0}\"  \"{1}\" ", inputFile.Filename, outputFile.Filename).ToString();

            // Media seek position
            if (conversionOptions.Seek != null)
                commandBuilder.AppendFormat(CultureInfo.InvariantCulture, " -ss {0} ", conversionOptions.Seek.Value.TotalSeconds);

            commandBuilder.AppendFormat(" -i \"{0}\" ", inputFile.Filename);

            // Physical media conversion (DVD etc)
            if (conversionOptions.Target != Target.Default)
            {
                commandBuilder.Append(" -target ");
                if (conversionOptions.TargetStandard != TargetStandard.Default)
                {
                    commandBuilder.AppendFormat(" {0}-{1} \"{2}\" ", conversionOptions.TargetStandard.ToLower(),
                        conversionOptions.Target.ToLower(), outputFile.Filename);

                    return commandBuilder.ToString();
                }
                commandBuilder.AppendFormat("{0} \"{1}\" ", conversionOptions.Target.ToLower(), outputFile.Filename);

                return commandBuilder.ToString();
            }

            // Audio sample rate
            if (conversionOptions.AudioSampleRate != AudioSampleRate.Default)
                commandBuilder.AppendFormat(" -ar {0} ", conversionOptions.AudioSampleRate.Remove("Hz"));

            // Maximum video duration
            if (conversionOptions.MaxVideoDuration != null)
                commandBuilder.AppendFormat(" -t {0} ", conversionOptions.MaxVideoDuration);

            // Video bit rate
            if (conversionOptions.VideoBitRate != null)
                commandBuilder.AppendFormat(" -b {0}k ", conversionOptions.VideoBitRate);

            // Video size / resolution
            if (conversionOptions.VideoSize == VideoSize.Custom)
            {
                commandBuilder.AppendFormat(" -vf \"scale={0}:{1}\" ", conversionOptions.CustomWidth ?? -2, conversionOptions.CustomHeight ?? -2);
            }
            else if (conversionOptions.VideoSize != VideoSize.Default)
            {
                string size = conversionOptions.VideoSize.ToLower();
                if (size.StartsWith("_")) size = size.Replace("_", "");
                if (size.Contains("_")) size = size.Replace("_", "-");

                commandBuilder.AppendFormat(" -s {0} ", size);
            }

            // Video aspect ratio
            if (conversionOptions.VideoAspectRatio != VideoAspectRatio.Default)
            {
                string ratio = conversionOptions.VideoAspectRatio.ToString();
                ratio = ratio.Substring(1);
                ratio = ratio.Replace("_", ":");

                commandBuilder.AppendFormat(" -aspect {0} ", ratio);
            }

            // Video cropping
            if (conversionOptions.SourceCrop != null)
            {
                var crop = conversionOptions.SourceCrop;
                commandBuilder.AppendFormat(" -filter:v \"crop={0}:{1}:{2}:{3}\" ", crop.Width, crop.Height, crop.X, crop.Y);
            }

            if (conversionOptions.BaselineProfile)
            {
                commandBuilder.Append(" -profile:v baseline ");
            }

            return commandBuilder.AppendFormat(" \"{0}\" ", outputFile.Filename).ToString();
        }
    }
}