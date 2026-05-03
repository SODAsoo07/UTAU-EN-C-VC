using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OpenUtau.Api;
using OpenUtau.Core.G2p;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Plugin.EnglishCplusVC {
    [Phonemizer("English C+VC Phonemizer", "EN C+VC", "Codex", language: "EN")]
    public class EnglishCplusVCPhonemizer : Phonemizer {
        private static readonly Dictionary<string, string[]> ConsonantFallbacks = new(StringComparer.Ordinal) {
            ["dd"] = new[] { "d", "t" },
            ["hh"] = new[] { "h" },
            ["zh"] = new[] { "sh", "z", "j" },
            ["dh"] = new[] { "d", "th", "z" },
            ["th"] = new[] { "t", "s" },
            ["v"] = new[] { "b", "f" },
            ["z"] = new[] { "s" },
            ["ng"] = new[] { "n", "m" },
            ["ch"] = new[] { "sh", "t" },
            ["j"] = new[] { "d", "z" },
            ["r"] = new[] { "l" },
        };

        private static readonly Dictionary<string, string[]> CodaFallbacks = new(StringComparer.Ordinal) {
            ["b"] = new[] { "p" },
            ["d"] = new[] { "t" },
            ["g"] = new[] { "k" },
            ["z"] = new[] { "s" },
            ["zh"] = new[] { "sh", "s" },
            ["dh"] = new[] { "th", "t" },
            ["v"] = new[] { "f" },
            ["j"] = new[] { "t" },
            ["ch"] = new[] { "t" },
            ["r"] = new[] { "l" },
            ["ng"] = new[] { "n", "m" },
        };

        private static readonly Dictionary<string, string[]> VowelFallbacks = new(StringComparer.Ordinal) {
            ["x3"] = new[] { "3", "x", "u", "e" },
            ["i3"] = new[] { "i", "E", "3", "e" },
            ["e3"] = new[] { "e", "E", "3" },
            ["o3"] = new[] { "o", "3", "6", "u" },
            ["x"] = new[] { "u", "@", "e" },
            ["@"] = new[] { "e", "a" },
            ["9"] = new[] { "o", "a", "u" },
            ["6"] = new[] { "u", "o" },
            ["A"] = new[] { "e", "E", "a" },
            ["O"] = new[] { "o", "u" },
            ["I"] = new[] { "a", "E", "i" },
            ["8"] = new[] { "a", "o", "u" },
            ["Q"] = new[] { "o", "u", "a" },
            ["3"] = new[] { "e", "u" },
        };

        private static readonly HashSet<string> Vowels = new(StringComparer.Ordinal) {
            "a","@","u","x","9","e","3","i","E","6","o","A","O","I","8","Q",
            "x3","i3","e3","o3",
        };

        private static readonly HashSet<string> Consonants = new(StringComparer.Ordinal) {
            "p","b","t","d","k","g","f","v","th","dh","s","z","sh","zh","h","ch","j",
            "m","n","ng","l","r","w","y","dd","hh",
        };

        // ARPAbet -> Cz VCCV notation
        private static readonly Dictionary<string, string> ArpaToCz = new(StringComparer.OrdinalIgnoreCase) {
            ["aa"] = "a",
            ["ae"] = "@",
            ["ah"] = "u",
            ["ax"] = "x",
            ["ao"] = "9",
            ["eh"] = "e",
            ["er"] = "3",
            ["ih"] = "i",
            ["iy"] = "E",
            ["uh"] = "6",
            ["uw"] = "o",
            ["ey"] = "A",
            ["ow"] = "O",
            ["ay"] = "I",
            ["aw"] = "8",
            ["oy"] = "Q",
            ["jh"] = "j",
            ["hh"] = "h",
            ["dx"] = "d",
            ["ix"] = "i",
            ["ux"] = "6",
            ["q"] = "t",
            ["axh"] = "x",
            ["bcl"] = "b",
            ["dcl"] = "d",
            ["gcl"] = "g",
            ["kcl"] = "k",
            ["pcl"] = "p",
            ["tcl"] = "t",
            ["hv"] = "h",
            ["nx"] = "n",
        };

        // Stress-aware ARPAbet mapping for frequent reduced vowels.
        private static readonly Dictionary<string, string> ArpaToCzStress = new(StringComparer.OrdinalIgnoreCase) {
            ["ah0"] = "x",
            ["ah1"] = "u",
            ["ah2"] = "u",
            ["er0"] = "x3",
            ["er1"] = "3",
            ["er2"] = "3",
        };

        // ARPAbet symbols that map to multiple Cz units.
        private static readonly Dictionary<string, string[]> ArpaToCzMulti = new(StringComparer.OrdinalIgnoreCase) {
            ["dr"] = new[] { "d", "r" },
            ["tr"] = new[] { "t", "r" },
            ["el"] = new[] { "x", "l" },
            ["em"] = new[] { "x", "m" },
            ["en"] = new[] { "x", "n" },
            ["eng"] = new[] { "x", "ng" },
            ["axr"] = new[] { "x3" },
            ["ehr"] = new[] { "e3" },
            ["eyr"] = new[] { "e3" },
            ["eir"] = new[] { "e3" },
            ["ihr"] = new[] { "i3" },
            ["iyr"] = new[] { "i3" },
            ["ir"] = new[] { "i3" },
            ["owr"] = new[] { "o3" },
            ["our"] = new[] { "o3" },
            ["aar"] = new[] { "a", "r" },
            ["ar"] = new[] { "a", "r" },
            ["aer"] = new[] { "@", "r" },
            ["ahr"] = new[] { "u", "r" },
            ["aor"] = new[] { "9", "r" },
            ["or"] = new[] { "9", "r" },
            ["awr"] = new[] { "8", "r" },
            ["aur"] = new[] { "8", "r" },
            ["ayr"] = new[] { "I", "r" },
            ["air"] = new[] { "I", "r" },
            ["oyr"] = new[] { "Q", "r" },
            ["oir"] = new[] { "Q", "r" },
            ["uhr"] = new[] { "6", "r" },
            ["uwr"] = new[] { "o", "r" },
            ["ur"] = new[] { "o", "r" },
        };

        private static readonly string[] DictionaryFileNames = {
            "en-cplusvc.yaml",
            "en-cPv.yaml",
            "arpasing.yaml",
        };
        private static readonly Regex InlinePhonemeGuideRegex = new(@"\[(?<phones>[^\[\]]+)\]", RegexOptions.Compiled);

        private IG2p g2p = new ArpabetG2p();
        private USinger? singer;

        public override void SetSinger(USinger singer) {
            this.singer = singer;
            LoadG2p();
        }

        public override Result Process(
            Note[] notes,
            Note? prev,
            Note? next,
            Note? prevNeighbour,
            Note? nextNeighbour,
            Note[] prevNeighbours) {
            var main = notes[0];

            if (!string.IsNullOrEmpty(main.lyric) && main.lyric.StartsWith("?")) {
                return MakeSimpleResult(main.lyric.Substring(1));
            }

            if (TryGetInlineGuideSymbols(main, out var inlineGuideSymbols)) {
                return ProcessSymbolSequence(notes, prevNeighbour, inlineGuideSymbols, forceExplicitTokens: true);
            }

            var currentSymbols = GetSymbols(main);
            if (currentSymbols.Length == 0) {
                return MakeSimpleResult(main.lyric);
            }

            if (CountVowels(currentSymbols) > 1) {
                return ProcessSymbolSequence(notes, prevNeighbour, currentSymbols, forceExplicitTokens: false);
            }

            var (onset, nucleus, coda) = SplitSyllable(currentSymbols);
            if (string.IsNullOrEmpty(nucleus)) {
                return MakeSimpleResult(main.lyric);
            }

            string? prevV = null;
            if (prevNeighbour.HasValue) {
                var prevSymbols = GetSymbols(prevNeighbour.Value);
                prevV = LastVowel(prevSymbols);
            }

            var duration = notes.Sum(n => n.duration);
            var tone = main.tone;
            var result = new List<PhonemeToken>();

            var pre = BuildLeadingAliases(prevV, onset, nucleus, tone);
            var preStep = MsToTickSafe(80);
            for (var i = 0; i < pre.Count; i++) {
                var pos = -preStep * (pre.Count - i);
                result.Add(new PhonemeToken(pre[i], pos));
            }

            var vowelAlias = BuildVowelAlias(prevV, onset, nucleus, tone);
            result.Add(new PhonemeToken(vowelAlias, 0));

            var ending = BuildEndingAliases(nucleus, coda, tone);
            if (ending.Count > 0) {
                var step = MsToTickSafe(70);
                var start = Math.Max(0, duration - MsToTickSafe(140 + (ending.Count - 1) * 70));
                for (var i = 0; i < ending.Count; i++) {
                    var pos = Math.Min(duration - 5, start + i * step);
                    result.Add(new PhonemeToken(ending[i], pos));
                }
            }

            return new Result {
                phonemes = result
                    .Where(p => !string.IsNullOrWhiteSpace(p.Alias))
                    .Select(p => new Phoneme { phoneme = p.Alias, position = p.Position })
                    .ToArray(),
            };
        }

        private Result ProcessSymbolSequence(Note[] notes, Note? prevNeighbour, string[] symbols, bool forceExplicitTokens) {
            if (symbols.Length == 0) {
                return MakeSimpleResult(notes[0].lyric);
            }

            var main = notes[0];
            var tone = main.tone;
            var duration = notes.Sum(n => n.duration);
            var leadStep = MsToTickSafe(70);
            var tokens = new List<PhonemeToken>();

            string? prevNeighbourVowel = null;
            if (prevNeighbour.HasValue) {
                var prevSymbols = GetSymbols(prevNeighbour.Value);
                prevNeighbourVowel = LastVowel(prevSymbols);
            }

            var firstVowelIndex = Array.FindIndex(symbols, IsVowel);
            if (firstVowelIndex < 0) {
                firstVowelIndex = symbols.Length;
            }

            // Leading consonants before the first vowel.
            for (var i = 0; i < firstVowelIndex; i++) {
                var prevSymbol = i > 0 ? symbols[i - 1] : null;
                var alias = ResolveSequenceAlias(prevSymbol, symbols[i], prevNeighbourVowel, tone, isFirst: i == 0, forceExplicitTokens);
                var pos = -leadStep * (firstVowelIndex - i);
                tokens.Add(new PhonemeToken(alias, pos));
            }

            // Main body from the first vowel to the end.
            var bodyCount = symbols.Length - firstVowelIndex;
            if (bodyCount > 0) {
                var bodyStep = bodyCount > 1 ? Math.Max(5, duration / (bodyCount - 1)) : 0;
                for (var j = 0; j < bodyCount; j++) {
                    var i = firstVowelIndex + j;
                    var prevSymbol = i > 0 ? symbols[i - 1] : null;
                    var alias = ResolveSequenceAlias(prevSymbol, symbols[i], prevNeighbourVowel, tone, isFirst: i == 0, forceExplicitTokens);
                    var pos = j == 0 ? 0 : Math.Min(duration - 5, j * bodyStep);
                    tokens.Add(new PhonemeToken(alias, pos));
                }
            } else if (tokens.Count > 0) {
                // Pure consonant guide: keep the last symbol at note start.
                var last = tokens[^1];
                tokens[^1] = last with { Position = 0 };
            }

            return new Result {
                phonemes = tokens
                    .Where(p => !string.IsNullOrWhiteSpace(p.Alias))
                    .Select(p => new Phoneme { phoneme = p.Alias, position = p.Position })
                    .ToArray(),
            };
        }

        private string ResolveSequenceAlias(string? prevSymbol, string currentSymbol, string? prevNeighbourVowel, int tone, bool isFirst, bool forceExplicitTokens) {
            var literal = currentSymbol?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(literal)) {
                return string.Empty;
            }

            // In forced inline-guide mode, explicit alias tokens are forced as-is.
            if (forceExplicitTokens && IsExplicitInlineAliasToken(literal)) {
                return literal;
            }

            // If the literal alias exists, use it directly.
            if (HasOto(literal, tone)) {
                return literal;
            }

            // Otherwise use symbol-aware fallback search.
            return PickInlineGuideAlias(prevSymbol, literal, prevNeighbourVowel, tone, isFirst);
        }

        private List<string> BuildLeadingAliases(string? prevV, string[] onset, string nucleus, int tone) {
            var aliases = new List<string>();
            if (onset.Length == 0) {
                return aliases;
            }

            var first = onset[0];
            var firstCandidates = ExpandConsonant(first).ToArray();
            if (!string.IsNullOrEmpty(prevV)) {
                var vv = new List<string>();
                foreach (var pv in ExpandVowel(prevV)) {
                    foreach (var c in firstCandidates) {
                        vv.Add($"{pv}{c}");
                        vv.Add($"{pv} {c}");
                    }
                }
                vv.AddRange(firstCandidates.SelectMany(c => new[] { $"- {c}", $"-{c}", $"_{c}" }));
                aliases.Add(PickAlias(tone, vv.ToArray()));
            } else {
                var head = new List<string>();
                foreach (var c in firstCandidates) {
                    head.Add($"- {c}");
                    head.Add($"-{c}");
                    head.Add($"_{c}");
                    head.Add($"{c}o");
                    head.Add($"{c}e");
                    head.Add($"{c}i");
                    head.Add($"{c}u");
                    head.Add($"{c}a");
                    head.Add(c);
                }
                aliases.Add(PickAlias(tone, head.ToArray()));
            }

            for (var i = 1; i < onset.Length; i++) {
                var c1 = onset[i - 1];
                var c2 = onset[i];
                var cc = new List<string>();
                foreach (var lc in ExpandConsonant(c1)) {
                    foreach (var rc in ExpandConsonant(c2)) {
                        cc.Add($"{lc}{rc}");
                        cc.Add($"{lc} {rc}");
                    }
                }
                cc.AddRange(ExpandConsonant(c2));
                aliases.Add(PickAlias(tone, cc.ToArray()));
            }
            return aliases;
        }

        private string BuildVowelAlias(string? prevV, string[] onset, string nucleus, int tone) {
            if (onset.Length == 0 && !string.IsNullOrEmpty(prevV)) {
                var vvCandidates = new List<string>();
                foreach (var pv in ExpandVowel(prevV)) {
                    foreach (var nv in ExpandVowel(nucleus)) {
                        vvCandidates.Add($"{pv}{nv}");
                        vvCandidates.Add($"{pv} {nv}");
                    }
                }
                foreach (var vv in vvCandidates.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()) {
                    if (HasOto(vv, tone)) {
                        return vv;
                    }
                }
            }
            var vCandidates = ExpandVowel(nucleus).Append("a").ToArray();
            return PickAlias(tone, vCandidates);
        }

        private List<string> BuildEndingAliases(string nucleus, string[] coda, int tone) {
            var aliases = new List<string>();
            if (coda.Length == 0) {
                return aliases;
            }

            var firstEnding = new List<string>();
            foreach (var v in ExpandVowel(nucleus)) {
                foreach (var c in ExpandCoda(coda[0])) {
                    firstEnding.Add($"{v}{c}");
                    firstEnding.Add($"{v}{c}-");
                    firstEnding.Add($"{v} {c}");
                    firstEnding.Add($"{v} {c}-");
                }
            }
            firstEnding.AddRange(ExpandCoda(coda[0]));
            aliases.Add(PickAlias(tone, firstEnding.ToArray()));

            for (var i = 1; i < coda.Length; i++) {
                var c1 = coda[i - 1];
                var c2 = coda[i];
                var cluster = new List<string>();
                foreach (var lc in ExpandCoda(c1)) {
                    foreach (var rc in ExpandCoda(c2)) {
                        cluster.Add($"{lc}{rc}");
                        cluster.Add($"{lc}{rc}-");
                        cluster.Add($"{lc} {rc}");
                        cluster.Add($"{lc} {rc}-");
                    }
                }
                cluster.AddRange(ExpandCoda(c2));
                aliases.Add(PickAlias(tone, cluster.ToArray()));
            }

            return aliases;
        }

        private string PickInlineGuideAlias(string? prevSymbol, string currentSymbol, string? prevNeighbourVowel, int tone, bool isFirst) {
            var candidates = new List<string>();
            if (IsExplicitInlineAliasToken(currentSymbol)) {
                candidates.AddRange(BuildExplicitInlineAliasCandidates(currentSymbol));
                return PickAlias(tone, candidates.ToArray());
            }

            if (isFirst) {
                if (IsConsonant(currentSymbol)) {
                    foreach (var c in ExpandConsonant(currentSymbol)) {
                        if (!string.IsNullOrEmpty(prevNeighbourVowel)) {
                            foreach (var pv in ExpandVowel(prevNeighbourVowel)) {
                                candidates.Add($"{pv}{c}");
                                candidates.Add($"{pv} {c}");
                            }
                        }
                        candidates.Add($"- {c}");
                        candidates.Add($"-{c}");
                        candidates.Add($"_{c}");
                        candidates.Add(c);
                    }
                    return PickAlias(tone, candidates.ToArray());
                }

                if (IsVowel(currentSymbol)) {
                    if (!string.IsNullOrEmpty(prevNeighbourVowel)) {
                        foreach (var pv in ExpandVowel(prevNeighbourVowel)) {
                            foreach (var cv in ExpandVowel(currentSymbol)) {
                                candidates.Add($"{pv}{cv}");
                                candidates.Add($"{pv} {cv}");
                            }
                        }
                    }
                    candidates.AddRange(ExpandVowel(currentSymbol));
                    candidates.Add("a");
                    return PickAlias(tone, candidates.ToArray());
                }
            }

            if (!string.IsNullOrEmpty(prevSymbol)) {
                if (IsVowel(prevSymbol) && IsConsonant(currentSymbol)) {
                    foreach (var pv in ExpandVowel(prevSymbol)) {
                        foreach (var c in ExpandCoda(currentSymbol)) {
                            candidates.Add($"{pv}{c}");
                            candidates.Add($"{pv} {c}");
                            candidates.Add($"{pv}{c}-");
                            candidates.Add($"{pv} {c}-");
                        }
                    }
                    candidates.AddRange(ExpandCoda(currentSymbol));
                    return PickAlias(tone, candidates.ToArray());
                }

                if (IsConsonant(prevSymbol) && IsConsonant(currentSymbol)) {
                    foreach (var lc in ExpandConsonant(prevSymbol)) {
                        foreach (var rc in ExpandConsonant(currentSymbol)) {
                            candidates.Add($"{lc}{rc}");
                            candidates.Add($"{lc} {rc}");
                            candidates.Add($"{lc}{rc}-");
                            candidates.Add($"{lc} {rc}-");
                        }
                    }
                    candidates.AddRange(ExpandConsonant(currentSymbol));
                    return PickAlias(tone, candidates.ToArray());
                }

                if (IsVowel(prevSymbol) && IsVowel(currentSymbol)) {
                    foreach (var pv in ExpandVowel(prevSymbol)) {
                        foreach (var cv in ExpandVowel(currentSymbol)) {
                            candidates.Add($"{pv}{cv}");
                            candidates.Add($"{pv} {cv}");
                        }
                    }
                    candidates.AddRange(ExpandVowel(currentSymbol));
                    return PickAlias(tone, candidates.ToArray());
                }

                if (IsConsonant(prevSymbol) && IsVowel(currentSymbol)) {
                    // This format avoids direct CV dependence; keep vowel-centric selection.
                    candidates.AddRange(ExpandVowel(currentSymbol));
                    candidates.Add("a");
                    return PickAlias(tone, candidates.ToArray());
                }
            }

            if (IsVowel(currentSymbol)) {
                return PickAlias(tone, ExpandVowel(currentSymbol).Append("a").ToArray());
            }
            return PickAlias(tone, ExpandConsonant(currentSymbol).ToArray());
        }

        private static bool IsExplicitInlineAliasToken(string token) {
            if (string.IsNullOrWhiteSpace(token)) {
                return false;
            }
            token = token.Trim();
            return token.StartsWith("-") || token.StartsWith("_") || token.EndsWith("-") || token.Contains(' ');
        }

        private static IEnumerable<string> BuildExplicitInlineAliasCandidates(string token) {
            if (string.IsNullOrWhiteSpace(token)) {
                yield break;
            }
            token = token.Trim();
            yield return token;

            if (token.StartsWith("-") && token.Length > 1) {
                var body = token.Substring(1).Trim();
                if (!string.IsNullOrWhiteSpace(body)) {
                    yield return $"- {body}";
                    yield return $"-{body}";
                    yield return $"_{body}";
                    yield return body;
                }
            }

            if (token.StartsWith("_") && token.Length > 1) {
                var body = token.Substring(1).Trim();
                if (!string.IsNullOrWhiteSpace(body)) {
                    yield return $"_{body}";
                    yield return $"- {body}";
                    yield return $"-{body}";
                    yield return body;
                }
            }

            if (token.EndsWith("-") && token.Length > 1) {
                var body = token.Substring(0, token.Length - 1).Trim();
                if (!string.IsNullOrWhiteSpace(body)) {
                    yield return $"{body}-";
                    yield return $"{body} -";
                    yield return body;
                }
            }
        }

        private static IEnumerable<string> ExpandConsonant(string consonant) {
            yield return consonant;
            if (ConsonantFallbacks.TryGetValue(consonant, out var fallbacks)) {
                foreach (var fb in fallbacks) {
                    yield return fb;
                }
            }
        }

        private static IEnumerable<string> ExpandCoda(string consonant) {
            yield return consonant;
            if (CodaFallbacks.TryGetValue(consonant, out var fallbacks)) {
                foreach (var fb in fallbacks) {
                    yield return fb;
                }
            } else if (ConsonantFallbacks.TryGetValue(consonant, out var generic)) {
                foreach (var fb in generic) {
                    yield return fb;
                }
            }
        }

        private static IEnumerable<string> ExpandVowel(string vowel) {
            yield return vowel;
            if (VowelFallbacks.TryGetValue(vowel, out var fallbacks)) {
                foreach (var fb in fallbacks) {
                    yield return fb;
                }
            }
        }

        private (string[] onset, string nucleus, string[] coda) SplitSyllable(string[] symbols) {
            var vIndex = Array.FindIndex(symbols, IsVowel);
            if (vIndex < 0) {
                return (symbols, string.Empty, Array.Empty<string>());
            }
            var nextVIndex = Array.FindIndex(symbols, vIndex + 1, IsVowel);
            var onset = symbols.Take(vIndex).Where(IsConsonant).ToArray();
            var nucleus = symbols[vIndex];
            var codaSource = nextVIndex >= 0
                ? symbols.Skip(vIndex + 1).Take(nextVIndex - vIndex - 1)
                : symbols.Skip(vIndex + 1);
            var coda = codaSource.Where(IsConsonant).ToArray();
            return (onset, nucleus, coda);
        }

        private string[] GetSymbols(Note note) {
            if (!string.IsNullOrWhiteSpace(note.phoneticHint)) {
                return SplitSymbolText(note.phoneticHint)
                    .SelectMany(NormalizeSymbols)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }

            var lyric = note.lyric?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(lyric)) {
                return Array.Empty<string>();
            }

            if (TryExtractInlinePhonemeGuide(lyric, out var inlineGuide, out var lyricWithoutGuide)) {
                if (inlineGuide.Length > 0 && inlineGuide.Any(IsKnownSymbol)) {
                    return inlineGuide;
                }
                lyric = lyricWithoutGuide;
                if (string.IsNullOrEmpty(lyric)) {
                    return Array.Empty<string>();
                }
            }

            var explicitSymbols = SplitSymbolText(lyric)
                .SelectMany(NormalizeSymbols)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            if (explicitSymbols.Length > 0 && explicitSymbols.All(IsKnownSymbol)) {
                return explicitSymbols;
            }

            foreach (var candidate in BuildG2pCandidates(lyric)) {
                if (TryQueryG2p(candidate, out var fromDict)) {
                    return fromDict;
                }
            }

            var tokenized = Regex.Matches(lyric, @"[A-Za-z']+")
                .Select(m => m.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();
            if (tokenized.Length > 1) {
                var merged = new List<string>();
                var allResolved = true;
                foreach (var token in tokenized) {
                    if (!TryQueryG2p(token, out var tokenSymbols)) {
                        allResolved = false;
                        break;
                    }
                    merged.AddRange(tokenSymbols);
                }
                if (allResolved && merged.Count > 0) {
                    return merged.ToArray();
                }
            }

            var literal = NormalizeSymbols(lyric)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            return literal.Length == 0 ? Array.Empty<string>() : literal;
        }

        private bool TryGetInlineGuideSymbols(Note note, out string[] symbols) {
            symbols = Array.Empty<string>();
            var lyric = note.lyric?.Trim() ?? string.Empty;
            var hasInlineGuide = lyric.Contains('[') && lyric.Contains(']');

            if (!hasInlineGuide) {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(note.phoneticHint)) {
                var fromHint = SplitInlineGuideText(note.phoneticHint)
                    .SelectMany(NormalizeSymbols)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                if (fromHint.Length > 0) {
                    symbols = fromHint;
                    return true;
                }
            }

            if (!TryExtractInlinePhonemeGuide(lyric, out var inlineGuide, out _)) {
                return false;
            }

            if (inlineGuide.Length == 0) {
                return false;
            }

            symbols = inlineGuide;
            return true;
        }

        private void LoadG2p() {
            var g2ps = new List<IG2p>();
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (singer != null && singer.Found && singer.Loaded && !string.IsNullOrWhiteSpace(singer.Location)) {
                foreach (var name in DictionaryFileNames) {
                    TryAddDictionary(Path.Combine(singer.Location, name), g2ps, loaded);
                }
            }

            if (!string.IsNullOrWhiteSpace(PluginDir)) {
                foreach (var name in DictionaryFileNames) {
                    TryAddDictionary(Path.Combine(PluginDir, name), g2ps, loaded);
                }
            }

            g2ps.Add(new ArpabetPlusG2p());
            g2ps.Add(new ArpabetG2p());
            g2p = g2ps.Count == 1 ? g2ps[0] : new G2pFallbacks(g2ps.ToArray());
        }

        private static void TryAddDictionary(string path, List<IG2p> g2ps, HashSet<string> loaded) {
            if (!File.Exists(path) || !loaded.Add(path)) {
                return;
            }
            try {
                g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());
            } catch {
                // Ignore invalid custom dictionary and continue fallback chain.
            }
        }

        private bool TryQueryG2p(string lyric, out string[] symbols) {
            symbols = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(lyric)) {
                return false;
            }
            var queried = g2p.Query(lyric.ToLowerInvariant());
            if (queried == null || queried.Length == 0) {
                return false;
            }
            var normalized = queried
                .SelectMany(NormalizeSymbols)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            if (normalized.Length == 0) {
                return false;
            }
            symbols = PostProcessG2pSymbols(normalized);
            return true;
        }

        private string[] PostProcessG2pSymbols(string[] symbols) {
            if (symbols.Length == 0) {
                return symbols;
            }

            var merged = new List<string>(symbols.Length);
            for (var i = 0; i < symbols.Length; i++) {
                var current = symbols[i];
                var next = i + 1 < symbols.Length ? symbols[i + 1] : null;
                var next2 = i + 2 < symbols.Length ? symbols[i + 2] : null;

                // Merge vowel + r in coda position to r-colored vowels.
                if (next == "r" && TryMergeRColored(current, next2, out var rColored)) {
                    merged.Add(rColored);
                    i++;
                    continue;
                }

                // American flap tendency for intervocalic t/d.
                if ((current == "t" || current == "d") &&
                    merged.Count > 0 &&
                    IsVowel(merged[^1]) &&
                    next != null &&
                    IsVowel(next)) {
                    merged.Add("dd");
                    continue;
                }

                merged.Add(current);
            }

            return merged.ToArray();
        }

        private bool TryMergeRColored(string vowel, string? afterR, out string merged) {
            merged = string.Empty;
            if (IsVowel(afterR ?? string.Empty)) {
                return false;
            }

            merged = vowel switch {
                "x" => "x3",
                "e" => "e3",
                "A" => "e3",
                "i" => "i3",
                "E" => "i3",
                "o" => "o3",
                "O" => "o3",
                "6" => "o3",
                _ => string.Empty,
            };
            return !string.IsNullOrEmpty(merged);
        }

        private static IEnumerable<string> BuildG2pCandidates(string lyric) {
            var candidates = new List<string>();
            var lower = lyric.ToLowerInvariant().Trim();
            if (!string.IsNullOrEmpty(lower)) {
                candidates.Add(lower);
            }

            var asciiClean = Regex.Replace(lower, @"[^a-z0-9'\- ]", "");
            if (!string.IsNullOrWhiteSpace(asciiClean)) {
                candidates.Add(asciiClean);
            }

            var noApos = Regex.Replace(asciiClean, @"['’]", "");
            if (!string.IsNullOrWhiteSpace(noApos)) {
                candidates.Add(noApos);
            }

            var noHyphen = noApos.Replace("-", " ");
            if (!string.IsNullOrWhiteSpace(noHyphen)) {
                candidates.Add(noHyphen);
            }

            var lettersOnly = Regex.Replace(noApos, @"[^a-z]", "");
            if (!string.IsNullOrWhiteSpace(lettersOnly)) {
                candidates.Add(lettersOnly);
            }

            return candidates
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal);
        }

        private static string[] SplitSymbolText(string text) {
            return Regex.Split(text, @"[\s,;/\+\-]+")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();
        }

        private static string[] SplitInlineGuideText(string text) {
            // Keep '-' and '_' inside tokens so users can force aliases like -g, _g, g-.
            return Regex.Split(text, @"[\s,;/\+]+")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();
        }

        private bool TryExtractInlinePhonemeGuide(string lyric, out string[] symbols, out string lyricWithoutGuide) {
            symbols = Array.Empty<string>();
            lyricWithoutGuide = lyric;
            if (string.IsNullOrWhiteSpace(lyric)) {
                return false;
            }

            var match = InlinePhonemeGuideRegex.Match(lyric);
            if (!match.Success) {
                return false;
            }

            var phoneText = match.Groups["phones"].Value;
            symbols = SplitInlineGuideText(phoneText)
                .SelectMany(NormalizeSymbols)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            lyricWithoutGuide = InlinePhonemeGuideRegex.Replace(lyric, " ").Trim();
            return true;
        }

        private IEnumerable<string> NormalizeSymbols(string symbol) {
            if (string.IsNullOrWhiteSpace(symbol)) {
                return Array.Empty<string>();
            }
            symbol = symbol.Trim();

            if (Vowels.Contains(symbol) || Consonants.Contains(symbol)) {
                return new[] { symbol };
            }

            var lower = symbol.ToLowerInvariant();
            if (Vowels.Contains(lower) || Consonants.Contains(lower)) {
                return new[] { lower };
            }

            if (ArpaToCzStress.TryGetValue(lower, out var stressMapped)) {
                return new[] { stressMapped };
            }

            var deStressed = Regex.Replace(lower, @"\d+$", "");
            if (Vowels.Contains(deStressed) || Consonants.Contains(deStressed)) {
                return new[] { deStressed };
            }

            if (ArpaToCzMulti.TryGetValue(deStressed, out var mappedMany)) {
                return mappedMany;
            }

            if (ArpaToCz.TryGetValue(deStressed, out var mapped)) {
                return new[] { mapped };
            }

            if (TrySplitCombinedSymbol(deStressed, out var split) && split.Length > 1) {
                return split;
            }

            return string.IsNullOrWhiteSpace(deStressed)
                ? Array.Empty<string>()
                : new[] { deStressed };
        }

        private bool TrySplitCombinedSymbol(string input, out string[] split) {
            split = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(input) || input.Length < 2) {
                return false;
            }

            var known = Vowels.Concat(Consonants)
                .OrderByDescending(s => s.Length)
                .ToArray();
            var result = new List<string>();
            var index = 0;

            while (index < input.Length) {
                string? matched = null;
                foreach (var candidate in known) {
                    if (index + candidate.Length <= input.Length &&
                        input.AsSpan(index, candidate.Length).SequenceEqual(candidate.AsSpan())) {
                        matched = candidate;
                        break;
                    }
                }
                if (matched == null) {
                    return false;
                }

                result.Add(matched);
                index += matched.Length;
            }

            if (result.Count == 0) {
                return false;
            }

            split = result.ToArray();
            return true;
        }

        private string? LastVowel(string[] symbols) {
            for (var i = symbols.Length - 1; i >= 0; i--) {
                if (IsVowel(symbols[i])) {
                    return symbols[i];
                }
            }
            return null;
        }

        private int CountVowels(string[] symbols) {
            var count = 0;
            foreach (var symbol in symbols) {
                if (IsVowel(symbol)) {
                    count++;
                }
            }
            return count;
        }

        private bool IsVowel(string symbol) => Vowels.Contains(symbol);
        private bool IsConsonant(string symbol) => Consonants.Contains(symbol);
        private bool IsKnownSymbol(string symbol) => IsVowel(symbol) || IsConsonant(symbol);

        private string PickAlias(int tone, params string[] candidates) {
            foreach (var candidate in candidates.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()) {
                if (HasOto(candidate, tone)) {
                    return candidate;
                }
            }
            return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;
        }

        private bool HasOto(string alias, int tone) {
            return singer != null && singer.TryGetMappedOto(alias, tone, out _);
        }

        private int MsToTickSafe(double ms) {
            var tick = timeAxis == null ? 0 : timeAxis.MsPosToTickPos(ms);
            return tick <= 0 ? 5 : tick;
        }

        private readonly record struct PhonemeToken(string Alias, int Position);
    }
}
