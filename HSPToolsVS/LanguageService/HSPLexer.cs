﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HSPToolsVS.LanguageService
{
    // ReSharper disable once InconsistentNaming
    internal class HSPLexer
    {
        private bool _isSpecialCharProcessing;
        private bool _isStringCharsIn;
        private int _line;
        private int _offset;
        private string _source;

        public void SetCurLine(string source, int line, int offset)
        {
            _source = source.Substring(offset);
            _line = line;
            _offset = offset;
            _isStringCharsIn = false;
            _isSpecialCharProcessing = false;
        }

        public Token GetNextToken(ref int state)
        {
            if (_source.Length <= _offset)
                return null;
            var source = _source.Substring(_offset);
            if (source == "")
                return null;

            var charHistory = new List<char>();
            foreach (var c in source)
            {
                if (!IsInBlockComment(state) && char.IsWhiteSpace(c))
                {
                    if (charHistory.Count != 0)
                        if (_isStringCharsIn)
                        {
                            charHistory.Add(c);
                            continue;
                        }
                        else
                            return ProduceToken(charHistory, true);
                    //  var index = _offset;
                    _offset += 1;
                    // return new Token("", _line, index, HSPTokenType.Sepatator);
                    continue;
                }
                if (c == ';')
                {
                    var index = _offset;
                    _offset += source.Length;
                    return new Token(source, _line, index, HSPTokenType.Comment);
                }
                if (c == '\'' || c == '"')
                    _isStringCharsIn = true;
                charHistory.Add(c);

                // Comments
                var str = string.Join(string.Empty, charHistory);
                if (IsInBlockComment(state))
                {
                    if (!str.EndsWith("*/"))
                        continue;
                    var index = _offset;
                    _offset += str.Length;
                    state = (int) ParseState.InNormal;
                    return new Token(str, _line, index, HSPTokenType.Comment);
                }
                if (!_isStringCharsIn && str == "/*")
                {
                    state = (int) ParseState.InBlockComment;
                    continue;
                }
                if (!_isStringCharsIn && str == "//")
                {
                    var index = _offset;
                    _offset += source.Length;
                    return new Token(source, _line, index, HSPTokenType.Comment);
                }
                var token = ProduceToken(charHistory);
                if (token != null)
                    return token;
            }
            if (IsInBlockComment(state))
            {
                var str = string.Join(string.Empty, charHistory);
                var index = _offset;
                _offset += str.Length;
                return new Token(str, _line, index, HSPTokenType.Comment);
            }
            return ProduceToken(charHistory, true);
        }

        private Token ProduceToken(List<char> charHistory, bool isForce = false)
        {
            var str = string.Join(string.Empty, charHistory);
            var index = _offset;
            // TODO: *flag の判定(* -> Operator, flag -> Identifier を *flag -> Flag にする)
            if (!_isStringCharsIn && str.Length > 1 && IsEndsWithInList(str, HSPTokens.Operators))
            {
                charHistory.RemoveRange(charHistory.Count - 1, 1);
                _isSpecialCharProcessing = true;
                return ProduceToken(charHistory, true);
            }
            if (_isSpecialCharProcessing && IsContainsInList(str, HSPTokens.Operators))
            {
                charHistory.Clear();
                _offset += str.Length;
                _isSpecialCharProcessing = false;
                return new Token(str, _line, index, HSPTokenType.Operator);
            }
            if (!_isStringCharsIn && str.Length > 1 && IsEndsWithInList(str, HSPTokens.Separators))
            {
                charHistory.RemoveRange(charHistory.Count - 1, 1);
                return ProduceToken(charHistory, true);
            }
            if (IsContainsInList(str, HSPTokens.Separators))
            {
                charHistory.Clear();
                _offset += str.Length;
                _isSpecialCharProcessing = false;
                return new Token(str, _line, index, HSPTokenType.Sepatator);
            }
            if (IsMatch(str, "^\".*?\"$"))
            {
                if (charHistory[charHistory.Count - 2] == '\\')
                    return null;
                charHistory.Clear();
                _isStringCharsIn = false;
                _offset += str.Length;
                return new Token(str, _line, index, HSPTokenType.String);
            }
            if (IsMatch(str, "^'.*?'$")) // HSP allows 'foo' (return 'f' char code).
            {
                charHistory.Clear();
                _isStringCharsIn = false;
                _offset += str.Length;
                return new Token(str, _line, index, HSPTokenType.Char);
            }
            if (!isForce)
                return null;
            charHistory.Clear();
            return ParseKeywordAndMacroAndPreprocessors(str) ?? ParseIdentifierOrNumericOrFlag(str);
        }

        private Token ParseKeywordAndMacroAndPreprocessors(string str)
        {
            var index = _offset;
            if (IsContainsInList(str, HSPTokens.Keywords))
            {
                _offset += str.Length;
                return new Token(str, _line, index, HSPTokenType.Keyword);
            }
            if (IsContainsInList(str, HSPTokens.Macros))
            {
                _offset += str.Length;
                return new Token(str, _line, index, HSPTokenType.Macro);
            }
            if (IsContainsInList(str, HSPTokens.Preprocessors))
            {
                _offset += str.Length;
                return new Token(str, _line, index, HSPTokenType.Preprocessor);
            }
            return null;
        }

        private Token ParseIdentifierOrNumericOrFlag(string str)
        {
            double d;
            var index = _offset;
            _offset += str.Length;
            if (double.TryParse(str, out d))
                return new Token(str, _line, index, HSPTokenType.Numeric);
            // Label
            if (str.StartsWith("*"))
            {
                return str.Length > 1
                    ? new Token(str, _line, index, HSPTokenType.Flag)
                    : new Token(str, _line, index, HSPTokenType.Operator);
            }
            return new Token(str, _line, index, HSPTokenType.Idenfitier);
        }

        private bool IsContainsInList(string str, params List<string>[] lists)
        {
            return lists.Any(list => list.Contains(str));
        }

        private bool IsEndsWithInList(string str, params List<string>[] lists)
        {
            return lists.Any(list => list.Any(str.EndsWith));
        }

        private bool IsMatch(string str, string regex)
        {
            return Regex.IsMatch(str, regex);
        }

        private bool IsInBlockComment(int state)
        {
            return state == (int) ParseState.InBlockComment;
        }
    }
}