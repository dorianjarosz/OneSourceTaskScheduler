﻿/** 
Copyright (c) 2009 Open Lab, http://www.open-lab.com/ 
Permission is hereby granted, free of charge, to any person obtaining 
a copy of this software and associated documentation files (the 
"Software"), to deal in the Software without restriction, including 
without limitation the rights to use, copy, modify, merge, publish, 
distribute, sublicense, and/or sell copies of the Software, and to 
permit persons to whom the Software is furnished to do so, subject to 
the following conditions: 
 
The above copyright notice and this permission notice shall be 
included in all copies or substantial portions of the Software. 
 
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OneSourceTaskScheduler.Security
{

    public class HtmlSanitizer
    {
        public static Regex forbiddenTags = new Regex("^(script|object|embed|link|style|form|input)$");
        public static Regex allowedTags = new Regex("^(b|p|i|s|a|img|table|thead|tbody|tfoot|tr|th|td|dd|dl|dt|em|h1|h2|h3|h4|h5|h6|li|ul|ol|span|div|strike|strong|" +
                "sub|sup|pre|del|code|blockquote|strike|kbd|br|hr|area|map|object|embed|param|link|form|small|big)$");

        private static Regex commentPattern = new Regex("<!--.*");  // <!--.........> 
        private static Regex tagStartPattern = new Regex("<(?i)(\\w+\\b)\\s*(.*)/?>$");  // <tag ....props.....> 
        private static Regex tagClosePattern = new Regex("</(?i)(\\w+\\b)\\s*>$");  // </tag .........> 

        private static Regex standAloneTags = new Regex("^(img|br|hr)$");
        private static Regex selfClosed = new Regex("<.+/>");

        private static Regex attributesPattern = new Regex("(\\w*)\\s*=\\s*\"([^\"]*)\"");  // prop="...." 
        private static Regex stylePattern = new Regex("([^\\s^:]+)\\s*:\\s*([^;]+);?");  // color:red; 

        private static Regex urlStylePattern = new Regex("(?i).*\\b\\s*url\\s*\\(['\"]([^)]*)['\"]\\)");  // url('....')" 

        public static Regex forbiddenStylePattern = new Regex("(?:(expression|eval|javascript))\\s*\\(");  // expression(....)"   thanks to Ben Summer 


        /** 
         * This method should be used to test input. 
         * 
         * @param html 
         * @return true if the input is "valid" 
         */
        public static bool isSanitized(string html)
        {
            return sanitizer(html).isValid;
        }


        /** 
         * Used to clean every html before to output it in any html page 
         * 
         * @param html 
         * @return sanitized html 
         */
        public static string sanitize(string html)
        {
            return sanitizer(html).html;
        }

        /** 
         * Used to get the text,  tags removed or encoded 
         * 
         * @param html 
         * @return sanitized text 
         */
        public static string getText(string html)
        {
            return sanitizer(html).text;
        }


        /** 
         * This is the main method of sanitizing. It will be used both for validation and cleaning 
         * 
         * @param html 
         * @return a SanitizeResult object 
         */
        public static SanitizeResult sanitizer(string html)
        {
            return sanitizer(html, allowedTags, forbiddenTags);
        }

        public static SanitizeResult sanitizer(string html, Regex allowedTags, Regex forbiddenTags)
        {
            SanitizeResult ret = new SanitizeResult();
            Stack<string> openTags = new Stack<string>();

            if (string.IsNullOrEmpty(html))
                return ret;

            List<string> tokens = tokenize(html);

            // -------------------   LOOP for every token -------------------------- 
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                bool isAcceptedToken = false;

                Match startMatcher = tagStartPattern.Match(token);
                Match endMatcher = tagClosePattern.Match(token);

                //--------------------------------------------------------------------------------  COMMENT    <!-- ......... --> 
                if (commentPattern.Match(token).Success)
                {
                    ret.val = ret.val + token + (token.EndsWith("-->") ? "" : "-->");
                    ret.invalidTags.Add(token + (token.EndsWith("-->") ? "" : "-->"));
                    continue;

                    //--------------------------------------------------------------------------------  OPEN TAG    <tag .........> 
                }
                else if (startMatcher.Success)
                {

                    //tag name extraction 
                    string tag = startMatcher.Groups[1].Value.ToLower();

                    //-----------------------------------------------------  FORBIDDEN TAG   <script .........> 
                    if (forbiddenTags.Match(tag).Success)
                    {
                        ret.invalidTags.Add("<" + tag + ">");
                        continue;

                        // --------------------------------------------------  WELL KNOWN TAG 
                    }
                    else if (allowedTags.Match(tag).Success)
                    {

                        string cleanToken = "<" + tag;
                        string tokenBody = startMatcher.Groups[2].Value;

                        //first test table consistency 
                        //table tbody tfoot thead th tr td 
                        if ("thead".Equals(tag) || "tbody".Equals(tag) || "tfoot".Equals(tag) || "tr".Equals(tag))
                        {
                            if (openTags.Select(t => t == "table").Count() <= 0)
                            {
                                ret.invalidTags.Add("<" + tag + ">");
                                continue;
                            }
                        }
                        else if ("td".Equals(tag) || "th".Equals(tag))
                        {
                            if (openTags.Count(t => t == "tr") <= 0)
                            {
                                ret.invalidTags.Add("<" + tag + ">");
                                continue;
                            }
                        }

                        // then test properties 
                        //Match attributes = attributesPattern.Match(tokenBody);
                        var attributes = attributesPattern.Matches(tokenBody);

                        bool foundURL = false; // URL flag

                        foreach (Match attribute in attributes)
                        //while (attributes.find())
                        {
                            string attr = attribute.Groups[1].Value.ToLower();
                            string val = attribute.Groups[2].Value;

                            // we will accept href in case of <A> 
                            if ("a".Equals(tag) && "href".Equals(attr))
                            {    // <a href="......">


                                try
                                {
                                    var url = new Uri(val);

                                    if (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeMailto)
                                    {
                                        foundURL = true;
                                    }
                                    else
                                    {
                                        ret.invalidTags.Add(attr + " " + val);
                                        val = "";
                                    }
                                }
                                catch
                                {
                                    ret.invalidTags.Add(attr + " " + val);
                                    val = "";
                                }
                            }
                            else if ((tag == "img" || tag == "embed") && "src".Equals(attr))
                            { // <img src="......"> 
                                try
                                {
                                    var url = new Uri(val);

                                    if (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
                                    {
                                        foundURL = true;
                                    }
                                    else
                                    {
                                        ret.invalidTags.Add(attr + " " + val);
                                        val = "";
                                    }
                                }
                                catch
                                {
                                    ret.invalidTags.Add(attr + " " + val);
                                    val = "";
                                }

                            }
                            else if ("href".Equals(attr) || "src".Equals(attr))
                            { // <tag src/href="......">   skipped 
                                ret.invalidTags.Add(tag + " " + attr + " " + val);
                                continue;

                            }
                            else if (attr == "width" || attr == "height")
                            { // <tag width/height="......">
                                Regex r = new Regex("\\d+%|\\d+$");
                                if (!r.Match(val.ToLower()).Success)
                                { // test numeric values 
                                    ret.invalidTags.Add(tag + " " + attr + " " + val);
                                    continue;
                                }

                            }
                            else if ("style".Equals(attr))
                            { // <tag style="......"> 

                                // then test properties 
                                var styles = stylePattern.Matches(val);
                                string cleanStyle = "";

                                foreach (Match style in styles)
                                //while (styles.find())
                                {
                                    string styleName = style.Groups[1].Value.ToLower();
                                    string styleValue = style.Groups[2].Value;

                                    // suppress invalid styles values 
                                    if (forbiddenStylePattern.Match(styleValue).Success)
                                    {
                                        ret.invalidTags.Add(tag + " " + attr + " " + styleValue);
                                        continue;
                                    }

                                    // check if valid url 
                                    Match urlStyleMatcher = urlStylePattern.Match(styleValue);
                                    if (urlStyleMatcher.Success)
                                    {
                                        try
                                        {
                                            string url = urlStyleMatcher.Groups[1].Value;
                                            var uri = new Uri(url);

                                            if (!(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                                            {
                                                ret.invalidTags.Add(tag + " " + attr + " " + styleValue);
                                                continue;
                                            }
                                        }
                                        catch
                                        {
                                            ret.invalidTags.Add(tag + " " + attr + " " + styleValue);
                                            continue;
                                        }

                                    }

                                    cleanStyle = cleanStyle + styleName + ":" + encode(styleValue) + ";";

                                }
                                val = cleanStyle;

                            }
                            else if (attr.StartsWith("on"))
                            {  // skip all javascript events 
                                ret.invalidTags.Add(tag + " " + attr + " " + val);
                                continue;

                            }
                            else
                            {  // by default encode all properies 
                                val = encode(val);
                            }

                            cleanToken = cleanToken + " " + attr + "=\"" + val + "\"";
                        }
                        cleanToken = cleanToken + ">";

                        isAcceptedToken = true;

                        // for <img> and <a>
                        if ((tag == "a" || tag == "img" || tag == "embed") && !foundURL)
                        {
                            isAcceptedToken = false;
                            cleanToken = "";
                        }

                        token = cleanToken;

                        // push the tag if require closure and it is accepted (otherwise is encoded) 
                        if (isAcceptedToken && !(standAloneTags.Match(tag).Success || selfClosed.Match(tag).Success))
                            openTags.Push(tag);

                        // --------------------------------------------------------------------------------  UNKNOWN TAG 
                    }
                    else
                    {
                        ret.invalidTags.Add(token);
                        ret.val = ret.val + token;
                        continue;

                    }

                    // --------------------------------------------------------------------------------  CLOSE TAG </tag> 
                }
                else if (endMatcher.Success)
                {
                    string tag = endMatcher.Groups[1].Value.ToLower();

                    //is self closing 
                    if (selfClosed.Match(tag).Success)
                    {
                        ret.invalidTags.Add(token);
                        continue;
                    }
                    if (forbiddenTags.Match(tag).Success)
                    {
                        ret.invalidTags.Add("/" + tag);
                        continue;
                    }
                    if (!allowedTags.Match(tag).Success)
                    {
                        ret.invalidTags.Add(token);
                        ret.val = ret.val + token;
                        continue;
                    }
                    else
                    {


                        string cleanToken = "";

                        // check tag position in the stack 

                        int pos = -1;
                        bool found = false;

                        foreach (var item in openTags)
                        {
                            pos++;
                            if (item == tag)
                            {
                                found = true;
                                break;
                            }
                        }

                        // if found on top ok 
                        if (found)
                        {
                            for (int k = 0; k <= pos; k++)
                            {
                                //pop all elements before tag and close it 
                                string poppedTag = openTags.Pop();
                                cleanToken = cleanToken + "</" + poppedTag + ">";
                                isAcceptedToken = true;
                            }
                        }

                        token = cleanToken;
                    }

                }

                ret.val = ret.val + token;

                if (isAcceptedToken)
                {
                    ret.html = ret.html + token;
                    //ret.text = ret.text + " "; 
                }
                else
                {
                    string sanToken = htmlEncodeApexesAndTags(token);
                    ret.html = ret.html + sanToken;
                    ret.text = ret.text + htmlEncodeApexesAndTags(removeLineFeed(token));
                }


            }

            // must close remaining tags 
            while (openTags.Count() > 0)
            {
                //pop all elements before tag and close it 
                string poppedTag = openTags.Pop();
                ret.html = ret.html + "</" + poppedTag + ">";
                ret.val = ret.val + "</" + poppedTag + ">";
            }

            //set boolean value 
            ret.isValid = ret.invalidTags.Count == 0;

            return ret;
        }

        /** 
         * Splits html tag and tag content <......>. 
         * 
         * @param html 
         * @return a list of token 
         */
        private static List<string> tokenize(string html)
        {
            //ArrayList tokens = new ArrayList();
            List<string> tokens = new List<string>();
            int pos = 0;
            string token = "";
            int len = html.Length;
            while (pos < len)
            {
                char c = html[pos];

                // BBB String ahead = html.Substring(pos, pos > len - 4 ? len : pos + 4);
                string ahead = html.Substring(pos, pos > len - 4 ? len - pos : 4);

                //a comment is starting 
                if ("<!--".Equals(ahead))
                {
                    //store the current token 
                    if (token.Length > 0)
                        tokens.Add(token);

                    //clear the token 
                    token = "";

                    // serch the end of <......> 
                    int end = moveToMarkerEnd(pos, "-->", html);

                    // BBB tokens.Add(html.Substring(pos, end));
                    tokens.Add(html.Substring(pos, end - pos));
                    pos = end;


                    // a new "<" token is starting 
                }
                else if ('<' == c)
                {

                    //store the current token 
                    if (token.Length > 0)
                        tokens.Add(token);

                    //clear the token 
                    token = "";

                    // serch the end of <......> 
                    int end = moveToMarkerEnd(pos, ">", html);
                    // BBB tokens.Add(html.Substring(pos, end));
                    tokens.Add(html.Substring(pos, end - pos));
                    pos = end;

                }
                else
                {
                    token = token + c;
                    pos++;
                }

            }

            //store the last token 
            if (token.Length > 0)
                tokens.Add(token);

            return tokens;
        }


        private static int moveToMarkerEnd(int pos, string marker, string s)
        {
            int i = s.IndexOf(marker, pos);
            if (i > -1)
                pos = i + marker.Length;
            else
                pos = s.Length;
            return pos;
        }

        /** 
         * Contains the sanitizing results. 
         * html is the sanitized html encoded  ready to be printed. Unaccepted tags are encode, text inside tag is always encoded   MUST BE USED WHEN PRINTING HTML 
         * text is the text inside valid tags. Contains invalid tags encoded                                                        SHOULD BE USED TO PRINT EXCERPTS 
         * val  is the html source cleaned from unaccepted tags. It is not encoded:                                                 SHOULD BE USED IN SAVE ACTIONS 
         * isValid is true when every tag is accepted without forcing encoding 
         * invalidTags is the list of encoded-killed tags 
         */
        public class SanitizeResult
        {
            public string html = "";
            public string text = "";
            public string val = "";
            public bool isValid = true;
            public List<string> invalidTags = new List<string>();
        }

        //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        public static string encode(string s)
        {
            return convertLineFeedToBR(htmlEncodeApexesAndTags(s == null ? "" : s));
        }

        public static string htmlEncodeApexesAndTags(string source)
        {
            return htmlEncodeTag(htmlEncodeApexes(source));
        }

        public static string htmlEncodeApexes(string source)
        {
            /*if (source != null)
            {
                String result = replaceAllNoRegex(source, new String[] { "&", "\"", "'" }, new String[] { "&amp;", "&quot;", "&#39;" });
                return result;
            }
            else
                return null;*/
            if (source != null)
            {
                string result = replaceAllNoRegex(source, new string[] { "\"", "'" }, new string[] { "&quot;", "&#39;" });
                return result;
            }
            else
                return null;
        }

        public static string htmlEncodeTag(string source)
        {
            if (source != null)
            {
                string result = replaceAllNoRegex(source, new string[] { "<", ">" }, new string[] { "&lt;", "&gt;" });
                return result;
            }
            else
                return null;
        }


        public static string convertLineFeedToBR(string text)
        {
            if (text != null)
                return replaceAllNoRegex(text, new string[] { "\n", "\f", "\r" }, new string[] { "<br>", "<br>", " " });
            else
                return null;
        }

        public static string removeLineFeed(string text)
        {

            if (text != null)
                return replaceAllNoRegex(text, new string[] { "\n", "\f", "\r" }, new string[] { " ", " ", " " });
            else
                return null;
        }



        public static string replaceAllNoRegex(string source, string[] searches, string[] replaces)
        {
            int k;
            string tmp = source;
            for (k = 0; k < searches.Length; k++)
                tmp = replaceAllNoRegex(tmp, searches[k], replaces[k]);
            return tmp;
        }

        public static string replaceAllNoRegex(string source, string search, string replace)
        {

            return source.Replace(search, replace);
        }
    }
}