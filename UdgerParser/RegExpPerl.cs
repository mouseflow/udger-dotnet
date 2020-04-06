/*

 Copyright (c) 2004-2006 Pavel Novak and Tomas Matousek.  

 The use and distribution terms for this software are contained in the file named License.txt, 
 which can be found in the root of the Phalanger distribution. By using this software 
 in any fashion, you are agreeing to be bound by the terms of this license.
 
 You must not remove this notice from this software.
*/
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Udger.Parser
{
    /// <summary>
    /// Perl regular expression specific options that are not captured by .NET <see cref="RegexOptions"/> or by
    /// transformation of the regular expression itself.
    /// </summary>
    [Flags]
    enum PerlRegexOptions
    {
        None = 0,
        Evaluate = 1,
        Ungreedy = 2,
        Anchored = 4,
        DollarMatchesEndOfStringOnly = 8,
        UTF8 = 16
    }

    #region PerlRegExpConverter

    /// <summary>
    /// Used for converting PHP Perl like regular expressions to .NET regular expressions.
    /// </summary>
    sealed class PerlRegExpConverter
    {
        #region Properties

        /// <summary>
        /// Regular expression used for matching quantifiers, they are changed ungreedy to greedy and vice versa if
        /// needed.
        /// </summary>
        private static Regex Quantifiers
        {
            get
            {
                if (_quantifiers == null)
                    _quantifiers = new Regex(@"\G(?:\?|\*|\+|\{[0-9]+,[0-9]*\})");

                return _quantifiers;
            }
        }
        private static Regex _quantifiers;

        /// <summary>
        /// Regular expression for POSIX regular expression classes matching.
        /// </summary>
        private static Regex PosixCharClasses
        {
            get
            {
                if (_posixCharClasses == null)
                    _posixCharClasses = new Regex("^\\[:(^)?(alpha|alnum|ascii|cntrl|digit|graph|lower|print|punct|space|upper|word|xdigit):]", RegexOptions.Singleline);

                return _posixCharClasses;
            }
        }
        private static Regex _posixCharClasses;

        /// <summary>
        /// Original perl regular expression passed to the constructor.
        /// </summary>
        private string perlRegEx;

        /// <summary>
        /// Returns <see cref="Regex"/> class that can be used for matching.
        /// </summary>
        public Regex Regex { get; private set; }

        /// <summary>
        /// .NET regular expression string. May be <B>null</B> if <see cref="Regex"/> is already set.
        /// </summary>
        private string dotNetMatchExpression;

        /// <summary>
        /// Returns .NET replacement string.
        /// </summary>
        public string DotNetReplaceExpression { get; }

        /// <summary>
        /// <see cref="RegexOptions"/> which should be set while matching the expression. May be <B>null</B>
        /// if <see cref="Regex"/> is already set.
        /// </summary>
        public RegexOptions DotNetOptions { get; private set; } = RegexOptions.None;

        public PerlRegexOptions PerlOptions { get; private set; } = PerlRegexOptions.None;

        public Encoding Encoding { get; }

        #endregion

        /// <summary>
        /// Creates new <see cref="PerlRegExpConverter"/> and converts Perl regular expression to .NET.
        /// </summary>
        /// <param name="pattern">Perl regular expression to convert.</param>
        /// <param name="replacement">Perl replacement string to convert or a <B>null</B> reference for match only.</param>
        /// <param name="encoding">Encoding used in the case the pattern is a binary string.</param>
        public PerlRegExpConverter(string pattern, string replacement, Encoding/*!*/ encoding)
        {
            Encoding = encoding
                ?? throw new ArgumentNullException(nameof(encoding));

            ConvertPattern(pattern);

            if (replacement != null)
                DotNetReplaceExpression = ConvertReplacement(replacement);
        }

        private void ConvertPattern(string pattern)
        {
            LoadPerlRegex(pattern);
            dotNetMatchExpression = ConvertRegex(perlRegEx, PerlOptions, Encoding);

            try
            {
                Regex = new Regex(dotNetMatchExpression, DotNetOptions);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(ExtractExceptionalMessage(e.Message));
            }
        }

        /// <summary>
        /// Extracts the .NET exceptional message from the message stored in an exception.
        /// The message has format 'parsing "{pattern}" - {message}\r\nParameter name {pattern}' in .NET 1.1.
        /// </summary>
        private string ExtractExceptionalMessage(string message)
        {
            if (message == null)
                return message;

            message = message.Replace(dotNetMatchExpression, "<pattern>");

            var i = message.IndexOf("\r\n");
            if (i >= 0)
                message = message.Substring(0, i);

            i = message.IndexOf("-");
            if (i >= 0)
                message = message.Substring(i + 2);

            return message;
        }

        internal string ConvertString(string str, int start, int length)
        {
            return (PerlOptions & PerlRegexOptions.UTF8) != 0
                ? Encoding.UTF8.GetString(Encoding.GetBytes(str.Substring(start, length)))
                : str.Substring(start, length);
        }

        private void LoadPerlRegex(string pattern)
        {
            if (pattern == null)
                pattern = "";

            var upattern = new StringBuilder();
            upattern.Append(pattern);

            FindRegexDelimiters(upattern, out var regexStart, out var regexEnd);
            ParseRegexOptions(upattern, regexEnd + 2, out var dotNetOptions, out var perlOptions);
            DotNetOptions = dotNetOptions;
            PerlOptions = perlOptions;

            perlRegEx = ConvertString(pattern, regexStart, regexEnd - regexStart + 1);
        }

        private static void FindRegexDelimiters(StringBuilder pattern, out int start, out int end)
        {
            var i = 0;
            while (i < pattern.Length && char.IsWhiteSpace(pattern[i])) i++;

            if (i == pattern.Length)
                throw new ArgumentException("RegExp empty");

            var startDelimiter = pattern[i++];
            if (char.IsLetterOrDigit(startDelimiter) || startDelimiter == '\\')
                throw new ArgumentException("Something bad with delimiter");

            start = i;
            char endDelimiter;
            if (startDelimiter == '[') endDelimiter = ']';
            else if (startDelimiter == '(') endDelimiter = ')';
            else if (startDelimiter == '{') endDelimiter = '}';
            else if (startDelimiter == '<') endDelimiter = '>';
            else endDelimiter = startDelimiter;

            var depth = 1;
            while (i < pattern.Length)
            {
                if (pattern[i] == '\\' && i + 1 < pattern.Length)
                {
                    i += 2;
                    continue;
                }

                if (pattern[i] == endDelimiter)   // (1) should precede (2) to handle end_delim == start_delim case
                {
                    depth--;
                    if (depth == 0)
                        break;
                }
                else if (pattern[i] == startDelimiter) // (2)
                {
                    depth++;
                }

                i++;
            }

            if (i == pattern.Length)
                throw new ArgumentException("No end delimiter");

            end = i - 1;
        }

        private static void ParseRegexOptions(StringBuilder pattern, int start,
          out RegexOptions dotNetOptions, out PerlRegexOptions extraOptions)
        {
            dotNetOptions = RegexOptions.None;
            extraOptions = PerlRegexOptions.None;

            for (var i = start; i < pattern.Length; i++)
            {
                var option = pattern[i];

                switch (option)
                {
                    case 'i': // PCRE_CASELESS
                        dotNetOptions |= RegexOptions.IgnoreCase;
                        break;

                    case 'm': // PCRE_MULTILINE
                        dotNetOptions |= RegexOptions.Multiline;
                        break;

                    case 's': // PCRE_DOTALL
                        dotNetOptions |= RegexOptions.Singleline;
                        break;

                    case 'x': // PCRE_EXTENDED
                        dotNetOptions |= RegexOptions.IgnorePatternWhitespace;
                        break;

                    case 'e': // evaluate as PHP code
                        extraOptions |= PerlRegexOptions.Evaluate;
                        break;

                    case 'A': // PCRE_ANCHORED
                        extraOptions |= PerlRegexOptions.Anchored;
                        break;

                    case 'D': // PCRE_DOLLAR_ENDONLY
                        extraOptions |= PerlRegexOptions.DollarMatchesEndOfStringOnly;
                        break;

                    case 'S': // spend more time studythe pattern - ignore
                        break;

                    case 'U': // PCRE_UNGREEDY
                        extraOptions |= PerlRegexOptions.Ungreedy;
                        break;

                    case 'u': // PCRE_UTF8
                        extraOptions |= PerlRegexOptions.UTF8;
                        break;
                }
            }

            // inconsistent options check:
            if ((dotNetOptions & RegexOptions.Multiline) != 0 &&
                (extraOptions & PerlRegexOptions.DollarMatchesEndOfStringOnly) != 0)
            {
                throw new Exception("Modifier inconsistent");
            }
        }

        private static int AlphaNumericToDigit(char x)
        {
            switch (x)
            {
                case '0':
                    return 0;
                case '1':
                    return 1;
                case '2':
                    return 2;
                case '3':
                    return 3;
                case '4':
                    return 4;
                case '5':
                    return 5;
                case '6':
                    return 6;
                case '7':
                    return 7;
                case '8':
                    return 8;
                case '9':
                    return 9;
                case 'A':
                    return 10;
                case 'B':
                    return 11;
                case 'C':
                    return 12;
                case 'D':
                    return 13;
                case 'E':
                    return 14;
                case 'F':
                    return 15;
                default:
                    return 17;
            }
        }

        /// <summary>
        /// Parses escaped sequences: "\[xX][0-9A-Fa-f]{2}", "\[xX]\{[0-9A-Fa-f]{0,4}\}", "\[0-7]{3}", 
        /// "\[pP]{Unicode Category}"
        /// </summary>
        private static bool ParseEscapeCode(Encoding encoding, string str, ref int pos, ref char ch, ref bool escaped)
        {
            if (pos + 3 >= str.Length)
                return false;

            var number = 0;

            if (str[pos + 1] == 'x')
            {
                if (str[pos + 2] == '{')
                {
                    // hexadecimal number encoding a Unicode character:
                    var i = pos + 3;
                    while (i < str.Length && str[i] != '}' && number < char.MaxValue)
                    {
                        var digit = AlphaNumericToDigit(str[i]);
                        if (digit > 16)
                            return false;

                        number = (number << 4) + digit;
                        i++;
                    }

                    if (number > char.MaxValue || i >= str.Length)
                        return false;

                    pos = i;
                    ch = (char)number;
                    escaped = IsCharRegexSpecial(ch);
                }
                else
                {
                    // hexadecimal number encoding single-byte character:
                    for (var i = pos + 2; i < pos + 4; i++)
                    {
                        //Debug.Assert(i < str.Length);
                        var digit = AlphaNumericToDigit(str[i]);
                        if (digit > 16)
                            return false;

                        number = (number << 4) + digit;
                    }

                    pos += 3;

                    var chars = encoding.GetChars(new[] { (byte)number });
                    if (chars.Length == 1)
                        ch = chars[0];
                    else
                        ch = (char)number;

                    escaped = IsCharRegexSpecial(ch);
                }

                return true;
            }

            if (str[pos + 1] >= '0' && str[pos + 1] <= '7')
            {
                // octal number:
                for (var i = pos + 1; i < pos + 4; i++)
                {
                    //Debug.Assert(i < str.Length);
                    var digit = AlphaNumericToDigit(str[i]);
                    if (digit > 8) return false;
                    number = (number << 3) + digit;
                }
                pos += 3;
                ch = encoding.GetChars(new[] { (byte)number })[0];
                escaped = IsCharRegexSpecial(ch);

                return true;
            }

            if (str[pos + 1] == 'p' || str[pos + 1] == 'P')
            {
                var complement = str[pos + 1] == 'P';
                int catStart;

                if (str[pos + 2] == '{')
                {
                    catStart = !complement && str[pos + 3] == '^'
                        ? pos + 4
                        : pos + 3;
                }
                else
                {
                    catStart = pos + 2;
                }

                var catLength = str.Length;
                var catEnd = catStart + catLength - 1;

                // check closing brace:
                if (str[pos + 2] == '{' && (catEnd + 1 >= str.Length || str[catEnd + 1] != '}'))
                    return false;

                // TODO: custom categories on .NET 2?
                // Unicode category:
                // ?? if (complement) pos = pos;
                return false;
            }

            if (str[pos + 1] == 'X')
                return false;

            return false;
        }

        /// <summary>
        /// Characters that must be encoded in .NET regexp
        /// </summary>
        static char[] encodeChars = { '.', '$', '(', ')', '*', '+', '?', '[', ']', '{', '}', '\\', '^', '|' };

        /// <summary>
        /// Returns true if character needs to be escaped in .NET regex
        /// </summary>
        private static bool IsCharRegexSpecial(char ch)
        {
            return Array.IndexOf(encodeChars, ch) != -1;
        }

        /// <summary>
        /// Converts Perl match expression (only, without delimiters, options etc.) to .NET regular expression.
        /// </summary>
        /// <param name="perlExpr">Perl regular expression to convert.</param>
        /// <param name="opt">Regexp options - some of them must be processed by changes in match string.</param>
        /// <param name="encoding">Encoding.</param>
        /// <returns>Resulting .NET regular expression.</returns>
        private static string ConvertRegex(string perlExpr, PerlRegexOptions opt, Encoding/*!*/ encoding)
        {
            // Ranges in bracket expressions should be replaced with appropriate characters

            // assume no conversion will be performed, create string builder with exact length. Only in
            // case there is a range StringBuilder would be prolonged, +1 for Anchored
            var result = new StringBuilder(perlExpr.Length + 1);

            // Anchored means that the string should match only at the start of the string, add '^'
            // at the beginning if there is no one
            if ((opt & PerlRegexOptions.Anchored) != 0 && (perlExpr.Length == 0 || perlExpr[0] != '^'))
                result.Append('^');

            // set to true after a quantifier is matched, if there is second quantifier just behind the
            // first it is an error
            var lastQuantifier = false;

            // 4 means we're switching from 3 back to 2 - ie. "a-b-c" 
            // (we need to make a difference here because second "-" shouldn't be expanded)
            var leavingRange = false;

            var state = 0;
            var groupState = 0;

            var i = 0;
            while (i < perlExpr.Length)
            {
                var ch = perlExpr[i];

                var escaped = false;
                if (ch == '\\' && !ParseEscapeCode(encoding, perlExpr, ref i, ref ch, ref escaped))
                {
                    i++;
                    //Debug.Assert(i < perlExpr.Length, "Regex cannot end with backslash.");
                    ch = perlExpr[i];

                    // some characters (like '_') don't need to be escaped in .net
                    escaped = ch != '_';
                }

                switch (state)
                {
                    case 0: // outside of character class
                        if (escaped)
                        {
                            result.Append('\\');
                            result.Append(ch);
                            lastQuantifier = false;
                            break;
                        }

                        // In perl regexps, named groups are written like this: "(?P<name> ... )"
                        // If the group is starting here, we need to skip the 'P' character (see state 4)
                        switch (groupState)
                        {
                            case 0: groupState = (ch == '(') ? 1 : 0; break;
                            case 1: groupState = (ch == '?') ? 2 : 0; break;
                            case 2: if (ch == 'P') { i++; continue; } break;
                        }

                        if ((opt & PerlRegexOptions.Ungreedy) != 0)
                        {
                            // match quantifier ?,*,+,{n,m} at the position i:
                            var m = Quantifiers.Match(perlExpr, i);

                            // quantifier matched; quentifier '?' hasn't to be preceded by '(' - a grouping construct '(?'
                            if (m.Success && (m.Value != "?" || i == 0 || perlExpr[i - 1] != '('))
                            {
                                // two quantifiers: 
                                if (lastQuantifier)
                                    throw new ArgumentException("regexp_duplicate_quantifier");

                                // append quantifier:
                                result.Append(perlExpr, i, m.Length);
                                i += m.Length;

                                if (i < perlExpr.Length && perlExpr[i] == '?')
                                {
                                    // skip question mark to make the quantifier greedy:
                                    i++;
                                }
                                else if (i < perlExpr.Length && perlExpr[i] == '+')
                                {
                                    // TODO: we do not yet support possesive quantifiers
                                    //       so we just skip the attribute it and pray
                                    //       nobody will ever realize :-)
                                    i++;
                                }
                                else
                                {
                                    // add question mark to make the quantifier lazy:
                                    if (result.Length != 0 && result[result.Length - 1] == '?')
                                    {
                                        // HACK: Due to the issue in .NET regex we can't use "??" because it isn't interpreted correctly!!
                                        // (for example "^(ab)??$" matches with "abab", but it shouldn't!!)
                                    }
                                    else
                                        result.Append('?');
                                }

                                lastQuantifier = true;
                                continue;
                            }
                        }

                        lastQuantifier = false;

                        if (ch == '$' && (opt & PerlRegexOptions.DollarMatchesEndOfStringOnly) != 0)
                        {
                            // replaces '$' with '\z': 
                            result.Append(@"\z");
                            break;
                        }

                        if (ch == '[')
                            state = 1;

                        result.Append(ch);
                        break;

                    case 1: // first character of character class
                        if (escaped)
                        {
                            result.Append('\\');
                            result.Append(ch);
                            state = 2;
                            break;
                        }

                        // special characters:
                        if (ch == '^' || ch == ']' || ch == '-')
                        {
                            result.Append(ch);
                        }
                        else
                        {
                            // other characters are not consumed here, for example [[:space:]abc] will not match if the first
                            // [ is appended here.
                            state = 2;
                            goto case 2;
                        }
                        break;

                    case 2: // inside of character class
                        if (escaped)
                        {
                            result.Append('\\');
                            result.Append(ch);
                            leavingRange = false;
                            break;
                        }

                        if (ch == '-' && !leavingRange)
                        {
                            state = 3;
                            break;
                        }
                        leavingRange = false;

                        // posix character classes
                        var match = PosixCharClasses.Match(perlExpr.Substring(i), 0);
                        if (match.Success)
                        {
                            var chars = CountCharacterClass(match.Groups[2].Value);
                            if (chars == null)
                                throw new ArgumentException($"Unknown character class '{match.Groups[2].Value}'");

                            if (match.Groups[1].Value.Length > 0)
                                throw new ArgumentException("POSIX character classes negation not supported.");

                            result.Append(chars);
                            i += match.Length - 1; // +1 is added just behind the switch
                            break;
                        }

                        if (ch == ']')
                            state = 0;
                        if (ch == '-')
                            result.Append("\\x2d");
                        else
                            result.Append(ch);
                        break;

                    case 3: // range previous character was '-'
                        if (!escaped && ch == ']')
                        {
                            result.Append("-]");
                            state = 0;
                            break;
                        }

                        if (!CountRange(result[result.Length - 1], ch, out var range, out var error, encoding))
                        {
                            if (error != 1 || !CountUnicodeRange(result[result.Length - 1], ch, out range))
                                throw new ArgumentException("range_first_character_greater");
                        }
                        result.Append(EscapeBracketExpressionSpecialChars(range)); // left boundary is duplicated, but doesn't matter...
                        state = 2;
                        leavingRange = true;
                        break;
                }

                i++;
            }

            return result.ToString();
        }

        /// <summary>
        /// Escapes characters that have special meaning in bracket expression to make them ordinary characters.
        /// </summary>
        /// <param name="chars">String possibly containing characters with special meaning.</param>
        /// <returns>String with escaped characters.</returns>
        internal static string EscapeBracketExpressionSpecialChars(string chars)
        {
            var sb = new StringBuilder();

            foreach (var ch in chars)
            {
                switch (ch)
                {
                    // case '^': // not necessary, not at the beginning have no special meaning
                    case '\\':
                    case ']':
                    case '-':
                        sb.Append('\\');
                        goto default;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }


        /// <summary>
        /// Takes endpoints of a range and returns string containing appropriate characters.
        /// </summary>
        /// <param name="firstCharacter">First endpoint of a range.</param>
        /// <param name="secondCharacter">Second endpoint of a range.</param>
        /// <param name="characters">String containing all characters that are to be in the range.</param>
        /// <param name="result">Integer specifying an error. Value 1 means characters specified cannot
        /// be expressed in current encoding, value of 2 first character is greater than second.</param>
        /// <returns><B>True</B> if range was succesfuly counted, <B>false</B> otherwise.</returns>
        internal static bool CountRange(char firstCharacter, char secondCharacter, out string characters, out int result, Encoding encoding)
        {
            // initialize out parameters
            characters = null;
            result = 0;

            var chars = new char[2];
            chars[0] = firstCharacter;
            chars[1] = secondCharacter;

            byte[] twoBytes = new byte[encoding.GetMaxByteCount(2)];

            // convert endpoints and test if characters are "normal" - they can be stored in one byte
            if (encoding.GetBytes(chars, 0, 2, twoBytes, 0) != 2)
            {
                result = 1;
                return false;
            }

            if (twoBytes[0] > twoBytes[1])
            {
                result = 2;
                return false;
            }

            // array for bytes that will be converted to unicode string
            var bytes = new byte[twoBytes[1] - twoBytes[0] + 1];

            var i = 0;
            for (int ch = twoBytes[0]; ch <= twoBytes[1]; i++, ch++)
            {
                // casting to byte is OK, ch is always in byte range thanks to ch <= two_bytes[1] condition
                bytes[i] = (byte)ch;
            }

            characters = encoding.GetString(bytes, 0, i);
            return true;
        }

        /// <summary>
        /// Takes character class name and returns string containing appropriate characters.
        /// Returns <B>null</B> if has got unknown character class name.
        /// </summary>
        /// <param name="chClassName">Character class name.</param>
        /// <returns>String containing characters from character class.</returns>
        internal static string CountCharacterClass(string chClassName)
        {
            string ret = null;

            switch (chClassName)
            {
                case "alnum":
                    ret = @"\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nd}";
                    break;
                case "digit":
                    ret = @"\p{Nd}";
                    break;
                case "punct":
                    ret = @"\p{P}\p{S}";
                    break;
                case "alpha":
                    ret = @"\p{Ll}\p{Lu}\p{Lt}\p{Lo}";
                    break;
                case "graph":
                    ret = @"\p{L}\p{M}\p{N}\p{P}\p{S}";
                    break;
                case "space":
                    ret = @"\s";
                    break;
                case "blank":
                    ret = @" \t";
                    break;
                case "lower":
                    ret = @"\p{Ll}";
                    break;
                case "upper":
                    ret = @"\p{Lu}";
                    break;
                case "cntrl":
                    ret = @"\p{Cc}";
                    break;
                case "print":
                    ret = @"\p{L}\p{M}\p{N}\p{P}\p{S}\p{Zs}";
                    break;
                case "xdigit":
                    ret = @"abcdefABCDEF\d";
                    break;
                case "ascii":
                    ret = @"\u0000-\u007F";
                    break;
                case "word":
                    ret = @"_\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nd}";
                    break;
            }

            return ret;
        }

        /// <summary>
        /// Simple version of 'PosixRegExp.BracketExpression.CountRange' function. Generates string
        /// with all characters in specified range, but uses unicode encoding.
        /// </summary>
        /// <param name="f">Lower bound</param>
        /// <param name="t">Upper bound</param>
        /// <param name="range">Returned string</param>
        /// <returns>Returns false if lower bound is larger than upper bound</returns>
        private static bool CountUnicodeRange(char f, char t, out string range)
        {
            range = "";
            if (f > t)
                return false;

            var sb = new StringBuilder(t - f);
            for (var c = f; c <= t; c++)
                sb.Append(c);

            range = sb.ToString();

            return true;
        }

        internal static bool IsDigitGroupReference(string replacement, int i)
        {
            return (replacement[i] == '$' || replacement[i] == '\\') &&
              (i + 1 < replacement.Length && char.IsDigit(replacement, i + 1));
        }

        internal static bool IsParenthesizedGroupReference(string replacement, int i)
        {
            return replacement[i] == '$' && i + 3 < replacement.Length && replacement[i + 1] == '{' &&
                char.IsDigit(replacement, i + 2) && (
                    replacement[i + 3] == '}' ||
                    i + 4 < replacement.Length && replacement[i + 4] == '}' && char.IsDigit(replacement, i + 3));
        }

        /// <summary>
        /// Converts substitutions of the form \\xx to $xx (perl to .NET format).
        /// </summary>
        /// <param name="replacement">String possibly containing \\xx substitutions.</param>
        /// <returns>String with converted $xx substitution format.</returns>
        private string ConvertReplacement(string replacement)
        {
            var result = new StringBuilder();
            var groupNumbers = Regex.GetGroupNumbers();
            var maxNumber = groupNumbers.Length > 0 ? groupNumbers[groupNumbers.Length - 1] : 0;

            var i = 0;
            while (i < replacement.Length)
            {
                if (IsDigitGroupReference(replacement, i) || IsParenthesizedGroupReference(replacement, i))
                {
                    var add = 0;
                    i++;

                    if (replacement[i] == '{') { i++; add = 1; }

                    var number = replacement[i++] - '0';
                    if (i < replacement.Length && char.IsDigit(replacement, i))
                    {
                        number = number * 10 + replacement[i];
                        i++;
                    }

                    // insert only existing group references (others replaced with empty string):
                    if (number <= maxNumber)
                    {
                        result.Append('$');
                        result.Append('{');
                        result.Append(number.ToString());
                        result.Append('}');
                    }

                    i += add;
                }
                else if (replacement[i] == '$')
                {
                    // there is $ and it is not a substitution - duplicate it:
                    result.Append("$$");
                    i++;
                }
                else if (replacement[i] == '\\' && i + 1 < replacement.Length)
                {
                    if (replacement[i + 1] == '\\')
                    {
                        // two backslashes, replace with one:
                        result.Append('\\');
                        i += 2;
                    }
                    else
                    {
                        // backslash + some character, skip two characters
                        result.Append(replacement, i, 2);
                        i += 2;
                    }
                }
                else
                {
                    // no substitution, no backslash (or backslash at the end of string)
                    result.Append(replacement, i++, 1);
                }
            }

            return result.ToString();
        }
    }

    #endregion
}
