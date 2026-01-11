using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleProcess;

public class MergeCommon
{
    public static void MergeAss(AssData baseData, AssData[] mergeData, string mergeType, string[]? commentEventLineStyleHeader = null)
    {
        var baseEvents = baseData.Events;
        if (commentEventLineStyleHeader != null)
        {
            if (baseEvents is null)
            {
                throw new InvalidDataException("Base ASS has no events section.");
            }

            for (var i = 0; i < baseEvents.Collection.Count; i++)
            {
                var evt = baseEvents.Collection[i];
                if (!evt.IsDialogue) { continue; }

                foreach (var header in commentEventLineStyleHeader)
                {
                    var _syl = evt.Style.AsSpan();
                    if (_syl.Length > header.Length)
                    {
                        var _charNextHeader = evt.Style.AsSpan(header.Length, 1)[0];
                        if (_syl.StartsWith(header.AsSpan(), StringComparison.OrdinalIgnoreCase) && (_charNextHeader == '-' || _charNextHeader == '_'))
                        {
                            evt.IsDialogue = false;
                            baseEvents.Collection[i] = evt;
                            break;
                        }
                    }
                }
            }
        }

        foreach (var md in mergeData)
        {
            if (mergeType == "style" || mergeType == "all")
            {
                if (!baseData.Styles.Formats.SequenceEqual(md.Styles.Formats))
                {
                    throw new Exception("Different styles formats.");
                }

                foreach (var st in md.Styles.Names)
                {
                    var _add = true;
                    var mSyl = md.Styles.Collection.First(x => x.Name == st);
                    // should override when duplicate style name
                    if (!baseData.Styles.Names.Add(st))
                    {
                        foreach (var bSyl in baseData.Styles.Collection)
                        {
                            if (bSyl.Name == st)
                            {
                                if (!bSyl.Equals(mSyl)) { baseData.Styles.Collection.Remove(bSyl); }
                                else { _add = false; }
                                break;
                            }
                        }
                    }
                    if (_add) { baseData.Styles.Collection.Add(mSyl); }
                }
            }

            if (mergeType == "event" || mergeType == "all")
            {
                if (baseEvents is null)
                {
                    throw new InvalidDataException("Base ASS has no events section.");
                }

                var mergeEvents = md.Events ?? throw new InvalidDataException("Merge ASS has no events section.");
                if (!baseEvents.Formats.SequenceEqual(mergeEvents.Formats))
                {
                    throw new Exception("Different events formats.");
                }

                baseEvents.Collection.AddRange(mergeEvents.Collection);
            }
        }
    }


}
