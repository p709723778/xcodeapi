using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System;


namespace UnityEditor.iOS.Xcode
{
    enum TokenType
    {
        EOF,
        Invalid,
        String,
        QuotedString,
        Comment,
        
        Semicolon,  // ;
        Comma,      // ,
        Eq,         // =
        LParen,     // (
        RParen,     // )
        LBrace,     // {
        RBrace,     // }      
    }
    
    class Token
    {
        public TokenType type;
        
        // the line of the input stream the token starts in (0-based)
        public int line;
        
        // start and past-the-end positions of the token in the input stream
        public int begin, end;
    }
    
    class TokenList : List<Token>
    {
    }
    
    class Lexer
    {
        string text;
        int pos;
        int length;
        int line;

        public static TokenList Tokenize(string text)
        {
            var lexer = new Lexer();
            lexer.SetText(text);
            return lexer.ScanAll();
        }
        
        public void SetText(string text)
        {
            this.text = text + "    "; // to prevent out-of-bounds access during look ahead
            pos = 0;
            length = text.Length;
            line = 0;
        }
        
        public TokenList ScanAll()
        {
            var tokens = new TokenList();
            
            while (true)
            {
                var tok = new Token();
                ScanOne(tok);
                tokens.Add(tok);
                if (tok.type == TokenType.EOF)
                    break;
            }
            return tokens;
        }
        
        bool IsValidStringChar(char ch)
        {
            return Char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '/' ; // must correspond to PBXRegex.DontNeedQuotes
        }
        
        void UpdateNewlineStats(char ch)
        {
            if (ch == '\n')
                line++;
        }
        
        // tokens list is modified in the case when we add BrokenLine token and need to remove already
        // added tokens for the current line
        void ScanOne(Token tok)
        {
            while (true)
            {
                while (pos < length && Char.IsWhiteSpace(text[pos]))
                {
                    UpdateNewlineStats(text[pos]);
                    pos++;
                }
                
                if (pos >= length)
                {
                    tok.type = TokenType.EOF;
                    break;
                }
                
                char ch = text[pos];
                char ch2 = text[pos+1];
                
                if (ch == '\"')
                    ScanQuotedString(tok);
                else if (ch == '/' && ch2 == '*')
                    ScanMultilineComment(tok);
                else if (ch == '/' && ch2 == '/')
                    ScanComment(tok);
                else if (IsValidStringChar(ch))
                    ScanString(tok);
                else if (ScanOperator(tok))
                    return; // scanned
                else // invalid character; we might simply continue at this point
                    throw new Exception("Invalid PBX project (parsing line " + line + ")");
                return;
            }    
        }
        
        void ScanString(Token tok)
        {
            tok.type = TokenType.String;
            tok.begin = pos;
            while (pos < length && IsValidStringChar(text[pos]))
                pos++;
            tok.end = pos;
            tok.line = line;
        }
        
        void ScanQuotedString(Token tok)
        {
            tok.type = TokenType.QuotedString;
            tok.begin = pos;
            pos++;
            
            while (pos < length)
            {
                // ignore escaped quotes
                if (text[pos] == '\\' && text[pos+1] == '\"')
                {
                    pos += 2;
                    continue;
                }
            
                // note that we close unclosed quotes
                if (text[pos] == '\n' || text[pos] == '\"')
                    break;
                pos++;
            }
            UpdateNewlineStats(text[pos]);
            pos++;
            tok.end = pos;
            tok.line = line;
        }

        void ScanMultilineComment(Token tok)
        {
            tok.type = TokenType.Comment;
            tok.begin = pos;
            pos += 2;
            
            while (pos < length)
            {
                if (text[pos] == '*' && text[pos+1] == '/')
                    break;
                
                // we support multiline comments
                UpdateNewlineStats(text[pos]);
                pos++;
            }
            pos += 2;
            tok.end = pos;
            tok.line = line;
        }

        void ScanComment(Token tok)
        {
            tok.type = TokenType.Comment;
            tok.begin = pos;
            pos += 2;

            while (pos < length)
            {
                if (text[pos] == '\n')
                    break;
                pos++;
            }
            UpdateNewlineStats(text[pos]);
            pos++;
            tok.end = pos;
            tok.line = line;
        }
        
        bool ScanOperator(Token tok)
        {
            switch (text[pos])
            {
                case ';': return ScanOperatorSpecific(tok, TokenType.Semicolon);
                case ',': return ScanOperatorSpecific(tok, TokenType.Comma);
                case '=': return ScanOperatorSpecific(tok, TokenType.Eq);
                case '(': return ScanOperatorSpecific(tok, TokenType.LParen);
                case ')': return ScanOperatorSpecific(tok, TokenType.RParen);
                case '{': return ScanOperatorSpecific(tok, TokenType.LBrace);
                case '}': return ScanOperatorSpecific(tok, TokenType.RBrace);
                default: return false;
            } 
        }
        
        bool ScanOperatorSpecific(Token tok, TokenType type)
        {
            tok.type = type;
            tok.begin = pos;
            pos++;
            tok.end = pos;
            tok.line = line;
            return true;
        }
    }
    

} // namespace UnityEditor.iOS.Xcode