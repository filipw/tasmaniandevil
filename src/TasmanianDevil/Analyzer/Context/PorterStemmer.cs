namespace TasmanianDevil.Analyzer.Context;

/// <summary>
/// A compact, dependency-free implementation of the classic Porter stemming algorithm (1980), a
/// public-domain suffix-stripping stemmer for English. Used as an offline approximation of dictionary
/// lemmatization for context matching.
/// </summary>
internal static class PorterStemmer
{
    /// <summary>Returns the Porter stem of <paramref name="word"/> (assumed already lowercased).</summary>
    public static string Stem(string word)
    {
        if (word.Length <= 2)
        {
            return word;
        }

        var b = word.ToCharArray();
        var k = b.Length - 1;

        k = Step1Ab(b, k);
        k = Step1C(b, k);
        k = Step2(b, k);
        k = Step3(b, k);
        k = Step4(b, k);
        k = Step5(b, k);

        return new string(b, 0, k + 1);
    }

    // a consonant is a letter that is not a vowel, with 'y' being a consonant unless preceded by a consonant
    private static bool IsConsonant(char[] b, int i)
    {
        switch (b[i])
        {
            case 'a' or 'e' or 'i' or 'o' or 'u':
                return false;
            case 'y':
                return i == 0 || !IsConsonant(b, i - 1);
            default:
                return true;
        }
    }

    // measure: the number of consonant sequences between 0 and j
    private static int Measure(char[] b, int j)
    {
        var n = 0;
        var i = 0;
        while (true)
        {
            if (i > j)
            {
                return n;
            }

            if (!IsConsonant(b, i))
            {
                break;
            }

            i++;
        }

        i++;
        while (true)
        {
            while (true)
            {
                if (i > j)
                {
                    return n;
                }

                if (IsConsonant(b, i))
                {
                    break;
                }

                i++;
            }

            i++;
            n++;
            while (true)
            {
                if (i > j)
                {
                    return n;
                }

                if (!IsConsonant(b, i))
                {
                    break;
                }

                i++;
            }

            i++;
        }
    }

    // true if 0..j contains a vowel
    private static bool VowelInStem(char[] b, int j)
    {
        for (var i = 0; i <= j; i++)
        {
            if (!IsConsonant(b, i))
            {
                return true;
            }
        }

        return false;
    }

    // true if j and j-1 are the same consonant (double consonant)
    private static bool DoubleConsonant(char[] b, int j) =>
        j >= 1 && b[j] == b[j - 1] && IsConsonant(b, j);

    // true if i-2,i-1,i is consonant-vowel-consonant and the final consonant is not w, x or y
    private static bool Cvc(char[] b, int i)
    {
        if (i < 2 || !IsConsonant(b, i) || IsConsonant(b, i - 1) || !IsConsonant(b, i - 2))
        {
            return false;
        }

        var ch = b[i];
        return ch is not ('w' or 'x' or 'y');
    }

    private static bool EndsWith(char[] b, int k, string s)
    {
        var len = s.Length;
        if (len > k + 1)
        {
            return false;
        }

        for (var i = 0; i < len; i++)
        {
            if (b[k - len + 1 + i] != s[i])
            {
                return false;
            }
        }

        return true;
    }

    // replace the suffix ending at k with s; returns the new end index
    private static int SetTo(char[] b, ref int k, int stemEnd, string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            b[stemEnd + 1 + i] = s[i];
        }

        k = stemEnd + s.Length;
        return k;
    }

    private static int Step1Ab(char[] b, int k)
    {
        if (b[k] == 's')
        {
            if (EndsWith(b, k, "sses"))
            {
                k -= 2;
            }
            else if (EndsWith(b, k, "ies"))
            {
                k -= 2;
            }
            else if (b[k - 1] != 's')
            {
                k -= 1;
            }
        }

        if (EndsWith(b, k, "eed"))
        {
            if (Measure(b, k - 3) > 0)
            {
                k -= 1;
            }
        }
        else if ((EndsWith(b, k, "ed") && VowelInStem(b, k - 2)) ||
                 (EndsWith(b, k, "ing") && VowelInStem(b, k - 3)))
        {
            // strip the "ed"/"ing" suffix, then patch the stem back into a canonical form
            k = EndsWith(b, k, "ed") ? k - 2 : k - 3;
            if (EndsWith(b, k, "at") || EndsWith(b, k, "bl") || EndsWith(b, k, "iz"))
            {
                // "at" -> "ate", "bl" -> "ble", "iz" -> "ize" (append an 'e')
                SetTo(b, ref k, k, "e");
            }
            else if (DoubleConsonant(b, k))
            {
                var ch = b[k];
                if (ch is not ('l' or 's' or 'z'))
                {
                    k -= 1;
                }
            }
            else if (Measure(b, k) == 1 && Cvc(b, k))
            {
                k += 1;
                b[k] = 'e';
            }
        }

        return k;
    }

    private static int Step1C(char[] b, int k)
    {
        if (EndsWith(b, k, "y") && VowelInStem(b, k - 1))
        {
            b[k] = 'i';
        }

        return k;
    }

    // step 2/3/4 suffix tables, longest-first within each bucket so the first match is the longest
    private static readonly (string Suffix, string Replacement)[] Step2Rules =
    [
        ("ational", "ate"), ("tional", "tion"), ("enci", "ence"), ("anci", "ance"), ("izer", "ize"),
        ("bli", "ble"), ("alli", "al"), ("entli", "ent"), ("eli", "e"), ("ousli", "ous"),
        ("ization", "ize"), ("ation", "ate"), ("ator", "ate"), ("alism", "al"), ("iveness", "ive"),
        ("fulness", "ful"), ("ousness", "ous"), ("aliti", "al"), ("iviti", "ive"), ("biliti", "ble"),
        ("logi", "log"),
    ];

    private static readonly (string Suffix, string Replacement)[] Step3Rules =
    [
        ("icate", "ic"), ("ative", ""), ("alize", "al"), ("iciti", "ic"), ("ical", "ic"),
        ("ful", ""), ("ness", ""),
    ];

    // ordered longest-first so the first match is the longest, per the 1980 algorithm
    private static readonly string[] Step4Suffixes =
    [
        "ement", "ance", "ence", "able", "ible", "ment", "ant", "ent", "ism", "ate",
        "iti", "ous", "ive", "ize", "al", "er", "ic", "ou",
    ];

    private static int Step2(char[] b, int k) => ApplyLongest(b, k, Step2Rules, minMeasure: 0);

    private static int Step3(char[] b, int k) => ApplyLongest(b, k, Step3Rules, minMeasure: 0);

    private static int Step4(char[] b, int k)
    {
        if (k <= 0)
        {
            return k;
        }

        // "ion" is conditional on the stem ending in s or t, so it is handled ahead of the table
        if (EndsWith(b, k, "ion") && k >= 3 && (b[k - 3] == 's' || b[k - 3] == 't'))
        {
            return Measure(b, k - 3) > 1 ? k - 3 : k;
        }

        // longest matching suffix only: if its measure condition fails, the step does nothing
        foreach (var suffix in Step4Suffixes)
        {
            if (!EndsWith(b, k, suffix))
            {
                continue;
            }

            var stemEnd = k - suffix.Length;
            return stemEnd >= 0 && Measure(b, stemEnd) > 1 ? stemEnd : k;
        }

        return k;
    }

    // finds the longest matching suffix in the table and applies it if the stem's measure qualifies;
    // a failed condition ends the step without trying a shorter suffix, per the 1980 algorithm
    private static int ApplyLongest(char[] b, int k, (string Suffix, string Replacement)[] rules, int minMeasure)
    {
        var bestIndex = -1;
        var bestLength = 0;
        for (var i = 0; i < rules.Length; i++)
        {
            var suffix = rules[i].Suffix;
            if (suffix.Length > bestLength && EndsWith(b, k, suffix))
            {
                bestIndex = i;
                bestLength = suffix.Length;
            }
        }

        if (bestIndex < 0)
        {
            return k;
        }

        var (matched, replacement) = rules[bestIndex];
        var stemEnd = k - matched.Length;
        if (stemEnd < 0 || Measure(b, stemEnd) <= minMeasure)
        {
            return k;
        }

        SetTo(b, ref k, stemEnd, replacement);
        return k;
    }

    private static int Step5(char[] b, int k)
    {
        if (b[k] == 'e')
        {
            var a = Measure(b, k - 1);
            if (a > 1 || (a == 1 && !Cvc(b, k - 1)))
            {
                k -= 1;
            }
        }

        if (b[k] == 'l' && DoubleConsonant(b, k) && Measure(b, k) > 1)
        {
            k -= 1;
        }

        return k;
    }

}
