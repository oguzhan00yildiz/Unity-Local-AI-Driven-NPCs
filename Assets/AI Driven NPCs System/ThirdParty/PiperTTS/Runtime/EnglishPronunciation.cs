using System;
using System.Collections.Generic;
using System.Text;

namespace PiperTTS
{
    /// <summary>
    /// English pronunciation rules applied before the neural phonemizer.
    /// Piper voices are trained on eSpeak NG phonemes, so the output follows eSpeak's en-us style:
    /// short function words carry no stress, contractions stay contracted,
    /// and words missing from the dictionary are built from dictionary parts where possible.
    /// </summary>
    public static class EnglishPronunciation
    {
        // Contractions as spoken, so "don't" is not read as "do not"
        private static readonly Dictionary<string, string> Contractions = new Dictionary<string, string>
        {
            { "i'm", "ˈaɪm" }, { "i've", "ˈaɪv" }, { "i'll", "ˈaɪl" }, { "i'd", "ˈaɪd" },
            { "you're", "jʊɹ" }, { "you've", "juːv" }, { "you'll", "juːl" }, { "you'd", "juːd" },
            { "he's", "hiːz" }, { "he'll", "hiːl" }, { "he'd", "hiːd" },
            { "she's", "ʃiːz" }, { "she'll", "ʃiːl" }, { "she'd", "ʃiːd" },
            { "it's", "ɪts" }, { "it'll", "ˈɪɾəl" }, { "it'd", "ˈɪɾəd" },
            { "we're", "wɪɹ" }, { "we've", "wiːv" }, { "we'll", "wiːl" }, { "we'd", "wiːd" },
            { "they're", "ðɛɹ" }, { "they've", "ðeɪv" }, { "they'll", "ðeɪl" }, { "they'd", "ðeɪd" },
            { "that's", "ðæts" }, { "what's", "wʌts" }, { "who's", "huːz" }, { "where's", "wɛɹz" },
            { "how's", "haʊz" }, { "there's", "ðɛɹz" }, { "here's", "hɪɹz" }, { "let's", "lɛts" },
            { "can't", "kˈænt" }, { "won't", "wˈoʊnt" }, { "don't", "dˈoʊnt" }, { "doesn't", "dˈʌzənt" },
            { "didn't", "dˈɪdənt" }, { "isn't", "ˈɪzənt" }, { "aren't", "ˈɑːɹnt" }, { "wasn't", "wˈʌzənt" },
            { "weren't", "wˈɜːnt" }, { "haven't", "hˈævənt" }, { "hasn't", "hˈæzənt" }, { "hadn't", "hˈædənt" },
            { "wouldn't", "wˈʊdənt" }, { "shouldn't", "ʃˈʊdənt" }, { "couldn't", "kˈʊdənt" }, { "mustn't", "mˈʌsənt" },
            { "ain't", "ˈeɪnt" }, { "y'all", "jˈɔːl" }, { "ma'am", "mˈæm" }, { "o'clock", "əklˈɑːk" },
            { "ok", "oʊkˈeɪ" }, { "i", "ˈaɪ" },
        };

        // Short grammar words. eSpeak speaks them without stress inside a sentence.
        private static readonly HashSet<string> FunctionWords = new HashSet<string>
        {
            "a", "an", "the", "and", "but", "or", "nor", "so", "if", "as", "than",
            "of", "to", "in", "on", "at", "by", "for", "from", "with", "into", "onto", "upon",
            "me", "my", "you", "your", "he", "him", "his", "she", "her", "it", "its",
            "we", "us", "our", "they", "them", "their",
            "is", "am", "are", "was", "were", "be", "been", "has", "have", "had",
            "do", "does", "did", "will", "would", "shall", "should", "can", "could", "may", "might", "must",
        };

        private static readonly string[] LetterNames =
        {
            "eɪ", "biː", "siː", "diː", "iː", "ɛf", "dʒiː", "eɪtʃ", "aɪ", "dʒeɪ", "keɪ", "ɛl", "ɛm",
            "ɛn", "oʊ", "piː", "kjuː", "ɑːɹ", "ɛs", "tiː", "juː", "viː", "dˈʌbəljuː", "ɛks", "waɪ", "ziː",
        };

        // Contracted endings added to a dictionary word ("should've", "Aada'll")
        private static readonly (string suffix, string phonemes)[] ContractedEndings =
        {
            ("'ve", "v"), ("'ll", "l"), ("'d", "d"), ("'re", "ɚ"),
        };

        /// <summary>
        /// Returns the phonemes for one word, or null when only the neural model can guess it.
        /// <paramref name="lookup"/> finds a lowercase word in the dictionary.
        /// </summary>
        public static string Pronounce(string word, Func<string, string> lookup)
        {
            string lower = word.ToLowerInvariant();

            if (Contractions.TryGetValue(lower, out string ph))
                return ph;

            string found = lookup(lower);
            if (found != null)
                return FunctionWords.Contains(lower) ? RemoveStress(found) : found;

            // Acronyms such as NPC are read letter by letter
            if (IsAcronym(word))
                return SpellLetters(lower);

            // Possessive or "is" ("guard's", "Aada's")
            if (lower.EndsWith("'s") && lower.Length > 2)
            {
                string stem = Pronounce(word.Substring(0, word.Length - 2), lookup);
                return stem == null ? null : stem + SEnding(stem);
            }
            // Plural possessive ("guards'")
            if (lower.EndsWith("s'"))
                return Pronounce(word.Substring(0, word.Length - 1), lookup);

            foreach (var (suffix, ending) in ContractedEndings)
            {
                if (lower.EndsWith(suffix) && lower.Length > suffix.Length)
                {
                    string stem = Pronounce(word.Substring(0, word.Length - suffix.Length), lookup);
                    return stem == null ? null : stem + ending;
                }
            }

            return Compound(lower, lookup);
        }

        /// <summary>Splits an unknown word into two dictionary words ("elm" + "ford"). The second part gets secondary stress.</summary>
        private static string Compound(string lower, Func<string, string> lookup)
        {
            if (lower.Length < 6) return null;

            string best = null;
            int bestScore = 0;
            for (int i = 3; i <= lower.Length - 3; i++)
            {
                string left = lookup(lower.Substring(0, i));
                string right = lookup(lower.Substring(i));
                if (left == null || right == null) continue;

                int score = Math.Min(i, lower.Length - i);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = left + right.Replace('ˈ', 'ˌ');
                }
            }
            return best;
        }

        private static bool IsAcronym(string word)
        {
            if (word.Length < 2 || word.Length > 5) return false;
            foreach (char c in word)
                if (c < 'A' || c > 'Z') return false;
            return true;
        }

        private static string SpellLetters(string lower)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < lower.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                string name = LetterNames[lower[i] - 'a'];
                // The last letter carries the stress: "en pee ˈsee"
                sb.Append(i == lower.Length - 1 && name.IndexOf('ˈ') < 0 ? "ˈ" + name : name);
            }
            return sb.ToString();
        }

        /// <summary>The "-s" sound after a stem: "ᵻz" after hissing sounds, "s" after voiceless sounds, otherwise "z".</summary>
        private static string SEnding(string stem)
        {
            string s = stem.TrimEnd('ː');
            if (s.EndsWith("s") || s.EndsWith("z") || s.EndsWith("ʃ") || s.EndsWith("ʒ") || s.EndsWith("tʃ") || s.EndsWith("dʒ"))
                return "ᵻz";
            char last = s.Length > 0 ? s[s.Length - 1] : 'a';
            return "ptkfθ".IndexOf(last) >= 0 ? "s" : "z";
        }

        private static string RemoveStress(string phonemes)
        {
            return phonemes.Replace("ˈ", "").Replace("ˌ", "");
        }
    }
}
