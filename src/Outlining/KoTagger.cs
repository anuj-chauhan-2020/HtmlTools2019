﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.WebTools.Languages.Shared.ContentTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace HtmlTools
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IOutliningRegionTag))]
    [ContentType(HtmlContentTypeDefinition.HtmlContentType)]
    internal sealed class KoTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return buffer.Properties.GetOrCreateSingletonProperty(() => new KoTagger(buffer)) as ITagger<T>;
        }
    }

    internal sealed class KoTagger : ITagger<IOutliningRegionTag>
    {
        private readonly string startHide = "<!-- ko";     //the characters that start the outlining region
        private readonly string endHide = "/ko -->";       //the characters that end the outlining region
        private readonly ITextBuffer buffer;
        private ITextSnapshot snapshot;
        private List<Region> regions;
        private static readonly Regex regex = new Regex(@"ko (.*?)-->", RegexOptions.Compiled);

        public KoTagger(ITextBuffer buffer)
        {
            this.buffer = buffer;
            snapshot = buffer.CurrentSnapshot;
            regions = new List<Region>();
            this.buffer.Changed += BufferChanged;

            Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() => ReParse()), DispatcherPriority.ApplicationIdle, null);

            this.buffer.Changed += BufferChanged;
        }

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            List<Region> currentRegions = regions;
            ITextSnapshot currentSnapshot = this.snapshot;

            SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
            int startLineNumber = entire.Start.GetContainingLine().LineNumber;
            int endLineNumber = entire.End.GetContainingLine().LineNumber;
            foreach (Region region in currentRegions)
            {
                if (region.StartLine <= endLineNumber && region.EndLine >= startLineNumber)
                {
                    ITextSnapshotLine startLine = currentSnapshot.GetLineFromLineNumber(region.StartLine);
                    ITextSnapshotLine endLine = currentSnapshot.GetLineFromLineNumber(region.EndLine);

                    var snapshot = new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End);
                    Match match = regex.Match(snapshot.GetText());

                    string text = string.IsNullOrWhiteSpace(match.Groups[1].Value) ? "" : match.Groups[1].Value.Trim();
                    string hoverText = snapshot.GetText();

                    //the region starts at the beginning of the "<!-- ko ", and goes until the *end* of the line that contains the "/ko -->".
                    yield return new TagSpan<IOutliningRegionTag>(
                        snapshot,
                        new OutliningRegionTag(false, true, "<!-- ko -->...<!-- /ko -->", hoverText));
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private void BufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
            if (e.After != buffer.CurrentSnapshot)
            {
                return;
            }

            Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() => ReParse()), DispatcherPriority.ApplicationIdle, null);
        }

        private void ReParse()
        {
            ITextSnapshot newSnapshot = buffer.CurrentSnapshot;
            var newRegions = new List<Region>();

            //keep the current (deepest) partial region, which will have
            // references to any parent partial regions.
            PartialRegion currentRegion = null;

            foreach (ITextSnapshotLine line in newSnapshot.Lines)
            {
                int regionStart = -1;
                string text = line.GetText();

                //lines that contain a "[" denote the start of a new region.
                if ((regionStart = text.IndexOf(startHide, StringComparison.Ordinal)) != -1 || (regionStart = text.IndexOf(startHide.Replace(" ", string.Empty), StringComparison.Ordinal)) != -1)
                {
                    int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;
                    if (!TryGetLevel(text, regionStart, out int newLevel))
                    {
                        newLevel = currentLevel + 1;
                    }

                    //levels are the same and we have an existing region;
                    //end the current region and start the next
                    if (currentLevel == newLevel && currentRegion != null)
                    {
                        newRegions.Add(new Region()
                        {
                            Level = currentRegion.Level,
                            StartLine = currentRegion.StartLine,
                            StartOffset = currentRegion.StartOffset,
                            EndLine = line.LineNumber
                        });

                        currentRegion = new PartialRegion()
                        {
                            Level = newLevel,
                            StartLine = line.LineNumber,
                            StartOffset = regionStart,
                            PartialParent = currentRegion.PartialParent
                        };
                    }
                    //this is a new (sub)region
                    else
                    {
                        currentRegion = new PartialRegion()
                        {
                            Level = newLevel,
                            StartLine = line.LineNumber,
                            StartOffset = regionStart,
                            PartialParent = currentRegion
                        };
                    }
                }
                //lines that contain "]" denote the end of a region
                else if ((regionStart = text.IndexOf(endHide, StringComparison.Ordinal)) != -1 || (regionStart = text.IndexOf(endHide.Replace(" ", string.Empty), StringComparison.Ordinal)) != -1)
                {
                    int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;

                    if (!TryGetLevel(text, regionStart, out int closingLevel))
                    {
                        closingLevel = currentLevel;
                    }

                    //the regions match
                    if (currentRegion != null &&
                        currentLevel == closingLevel)
                    {
                        newRegions.Add(new Region()
                        {
                            Level = currentLevel,
                            StartLine = currentRegion.StartLine,
                            StartOffset = currentRegion.StartOffset,
                            EndLine = line.LineNumber
                        });

                        currentRegion = currentRegion.PartialParent;
                    }
                }
            }

            //determine the changed span, and send a changed event with the new spans
            var oldSpans =
                new List<Span>(regions.Select(r => AsSnapshotSpan(r, snapshot)
                    .TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive)
                    .Span));
            var newSpans =
                    new List<Span>(newRegions.Select(r => AsSnapshotSpan(r, newSnapshot).Span));

            var oldSpanCollection = new NormalizedSpanCollection(oldSpans);
            var newSpanCollection = new NormalizedSpanCollection(newSpans);

            //the changed regions are regions that appear in one set or the other, but not both.
            var removed =
            NormalizedSpanCollection.Difference(oldSpanCollection, newSpanCollection);

            int changeStart = int.MaxValue;
            int changeEnd = -1;

            if (removed.Count > 0)
            {
                changeStart = removed[0].Start;
                changeEnd = removed[removed.Count - 1].End;
            }

            if (newSpans.Count > 0)
            {
                changeStart = Math.Min(changeStart, newSpans[0].Start);
                changeEnd = Math.Max(changeEnd, newSpans[newSpans.Count - 1].End);
            }

            snapshot = newSnapshot;
            regions = newRegions;

            if (changeStart <= changeEnd)
            {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, Span.FromBounds(changeStart, changeEnd))));
            }
        }

        private static bool TryGetLevel(string text, int startIndex, out int level)
        {
            level = -1;
            if (text.Length > startIndex + 3)
            {
                if (int.TryParse(text.Substring(startIndex + 1), out level))
                {
                    return true;
                }
            }

            return false;
        }

        private static SnapshotSpan AsSnapshotSpan(Region region, ITextSnapshot snapshot)
        {
            ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(region.StartLine);
            ITextSnapshotLine endLine = (region.StartLine == region.EndLine) ? startLine
                 : snapshot.GetLineFromLineNumber(region.EndLine);
            return new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End);
        }
    }
}

