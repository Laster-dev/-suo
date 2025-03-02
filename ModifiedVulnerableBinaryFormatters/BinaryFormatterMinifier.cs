﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using suo_exploit_test.Helpers.ModifiedVulnerableBinaryFormatters;

namespace suo_exploit_test.Helpers
{
    public class BinaryFormatterMinifier
    {
        public static string FullTypeNameMinifier(string strFullTypeName, string strAssemblyName)
        {
            if (strAssemblyName == null || strFullTypeName == null)
                return strFullTypeName;

            // replacing spaces between things like:
            // Foo, Microsoft.IdentityModel, Version=3.5.0.0, PublicKeyToken=31bf3856ad364e35
            // clr-namespace:System.Diagnostics; assembly=system
            string strFullTypeName_noSpace = System.Text.RegularExpressions.Regex.Replace(strFullTypeName, @"([^\w])[\s]+([\w])", "$1$2");
            strFullTypeName_noSpace = System.Text.RegularExpressions.Regex.Replace(strFullTypeName_noSpace, @"([\w])[\s]+([^\w])", "$1$2");
            strFullTypeName_noSpace = System.Text.RegularExpressions.Regex.Replace(strFullTypeName_noSpace, @"([^\w])[\s]+([^\w])", "$1$2");


            string shortenedFullTypeName = System.Text.RegularExpressions.Regex.Replace(strFullTypeName_noSpace, @"\s*,\s*Version=[^,]+,\s*Culture=[^,]+,\s*PublicKeyToken=[a-z0-9]{16}", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

            try
            {
                var asm = Assembly.Load(strAssemblyName);
                if (asm.GetType(shortenedFullTypeName) !=null)
                {
                    strFullTypeName = shortenedFullTypeName;
                }
                    
            }
            catch
            {
                strFullTypeName = strFullTypeName_noSpace;
            }

            return strFullTypeName;
        }

        public static string AssemblyOrTypeNameMinifier(string strInput)
        {
            if (strInput == null)
                return strInput;
            
            if (!System.Text.RegularExpressions.Regex.IsMatch(strInput, @"[,]\s*Version=[^,]+,\s*Culture=[^,]+,\s*PublicKeyToken=[a-z0-9]{16}"))
            {
                // does not contain an assembly name
                return strInput;
            }

            bool isAssemblyString = false;
            if (System.Text.RegularExpressions.Regex.IsMatch(strInput, @"^[^,]+\s*[,]\s*Version=[^,]+,\s*Culture=[^,]+,\s*PublicKeyToken=[a-z0-9]{16}$",System.Text.RegularExpressions.RegexOptions.IgnoreCase| System.Text.RegularExpressions.RegexOptions.Multiline))
            {
                isAssemblyString = true;
            }

            // replacing spaces between things like:
            // Microsoft.IdentityModel, Version=3.5.0.0, PublicKeyToken=31bf3856ad364e35
            // clr-namespace:System.Diagnostics; assembly=system
            string strInput_noSpace = System.Text.RegularExpressions.Regex.Replace(strInput, @"([^\w])[\s]+([\w])", "$1$2");
            strInput_noSpace = System.Text.RegularExpressions.Regex.Replace(strInput_noSpace, @"([\w])[\s]+([^\w])", "$1$2");
            strInput_noSpace = System.Text.RegularExpressions.Regex.Replace(strInput_noSpace, @"([^\w])[\s]+([^\w])", "$1$2");

            if(IsValid(strInput_noSpace, isAssemblyString))
            {
                strInput = strInput_noSpace;
            }
            

            string strInput_simpleAsm = System.Text.RegularExpressions.Regex.Replace(strInput, @"[,]\s*Version=[^,]+,\s*Culture=[^,]+,\s*PublicKeyToken=[a-z0-9]{16}", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

            if (IsValid(strInput_simpleAsm, isAssemblyString))
            {
                strInput = strInput_simpleAsm;
            }else if (!isAssemblyString && strInput.Contains("mscorlib"))
            {
                // we know mscorlib can be used a lot
                string strInput_simpleCorlibAsm = System.Text.RegularExpressions.Regex.Replace(strInput, @"mscorlib\s*,\s*Version=[^,]+,\s*Culture=[^,]+,\s*PublicKeyToken=[a-z0-9]{16}", "mscorlib", System.Text.RegularExpressions.RegexOptions.IgnoreCase| System.Text.RegularExpressions.RegexOptions.Multiline);

                if (IsValid(strInput_simpleCorlibAsm, isAssemblyString))
                    strInput = strInput_simpleCorlibAsm;
            }

            if (strInput.Contains(",mscorlib"))
            {
                string strInput_removedMSCORLIB = strInput.Replace(",mscorlib", "");

                if (IsValid(strInput_removedMSCORLIB, isAssemblyString))
                    strInput = strInput_removedMSCORLIB;
            }

            return strInput;
        }

        private static bool IsValid(string strInput, bool isAssemblyString)
        {
            bool result = false;

            if (isAssemblyString)
            {
                try
                {
                    if (Assembly.Load(strInput) != null)
                        result = true;
                }
                catch { }
            }
            else
            {
                try
                {
                    if (Type.GetType(strInput) != null)
                        result = true;
                }
                catch { }
                
            }

            return result;
        }

        

        private static StringBuilder DataObjectRemovalTester(ref JArray jsonJArrayObj, string myApp, bool isErrOk, bool showInfo)
        {
            JArray origJsonJArrayObj = new JArray(jsonJArrayObj.ToList().ToArray());
            string json_shortened = origJsonJArrayObj.ToString();
            bool ruleComplete = false;

            StringBuilder sbSuccessResult = new StringBuilder();

            // remove a Data object
            ruleComplete = false;
            int externalCounter = 0;
            while (!ruleComplete)
            {
                int internalCounter = 0;

                foreach (JObject item in origJsonJArrayObj)
                {
                    internalCounter++;
                    JToken dataItem = item["Data"];
                    foreach (JProperty subDataItem in dataItem)
                    {
                        string subDataItemName = subDataItem.Name;
                        JToken subDataItemValue = subDataItem.Value;
                        JTokenType subDataItemType = subDataItem.Value.Type;
                        if (subDataItemName.Equals("$type"))
                        {
                            if (subDataItemValue.ToString().Equals("MessageEnd"))
                            {
                                ruleComplete = true;
                            }
                            break;
                        }
                    }

                    if (!ruleComplete && internalCounter > externalCounter)
                    {
                        var currentObjId = item["Data"]["objectId"];

                        string tempValue = item.ToString();
                        item.Remove();

                        if (currentObjId != null)
                        {
                            // we want to remove objects that have idRef == objectId of our removed item
                            List<JObject> refRremovalList = new List<JObject>();
                            foreach (JObject otherItems in origJsonJArrayObj)
                            {
                                if (otherItems["Data"]["idRef"] != null)
                                {
                                    if ((int)otherItems["Data"]["idRef"] == (int)currentObjId)
                                    {
                                        refRremovalList.Add(otherItems);
                                    }
                                }
                            }

                            foreach (JObject refObject in refRremovalList)
                            {
                                refObject.Remove();
                            }
                        }

                        if (CheckIfSuccess(origJsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                        {
                            // it is a success so we can remove it!
                            // we have to start from the beginning!
                            externalCounter = 0;
                            jsonJArrayObj = new JArray(origJsonJArrayObj.ToList().ToArray());
                            sbSuccessResult.AppendLine("Successful in removing:" + tempValue);
                        }
                        else
                        {
                            // we should not remove it
                            externalCounter = internalCounter;
                            origJsonJArrayObj = new JArray(jsonJArrayObj.ToList().ToArray());
                        }

                        break;
                    }
                }
            }


            return sbSuccessResult;
        }

        private static StringBuilder DataObjectNullifyTester(ref JArray jsonJArrayObj, string myApp, bool isErrOk, List<String> valueExclusionList, bool showInfo)
        {

            JArray origJsonJArrayObj = new JArray(jsonJArrayObj.ToList().ToArray());
            string json_shortened = origJsonJArrayObj.ToString();
            bool ruleComplete = false;

            StringBuilder sbSuccessResult = new StringBuilder();

            JObject nullJObject = JObject.Parse(@"{'Id': 0,
    'TypeName': 'ObjectNull',
    'Data': {
      '$type': 'ObjectNull',
      'nullCount': 0
}}");
            // remove a Data object
            ruleComplete = false;
            int externalCounter = 0;
            while (!ruleComplete)
            {
                int internalCounter = 0;

                foreach (JObject item in origJsonJArrayObj)
                {
                    internalCounter++;
                    bool isExcluded = false;
                    JToken dataItem = item["Data"];
                    foreach (JProperty subDataItem in dataItem)
                    {
                        string subDataItemName = subDataItem.Name;
                        JToken subDataItemValue = subDataItem.Value;
                        JTokenType subDataItemType = subDataItem.Value.Type;
                        if (subDataItemName.Equals("$type"))
                        {
                            if (subDataItemValue.ToString().Equals("MessageEnd"))
                            {
                                ruleComplete = true;
                            }
                            else if (subDataItemValue.ToString().Equals("ObjectNull"))
                            {
                                isExcluded = true;
                            }
                            break;
                        }
                    }

                    string subDataItemValueStringValue = "";

                    if (item["Data"]["value"] != null)
                    {
                        subDataItemValueStringValue = (string)item["Data"]["value"];
                    }

                    if (isExcluded || valueExclusionList.Contains(subDataItemValueStringValue))
                        continue;

                    if (!ruleComplete && internalCounter > externalCounter)
                    {
                        var currentObjId = item["Data"]["objectId"];

                        string tempValue = item.ToString();
                        item.AddAfterSelf(nullJObject);
                        item.Remove();

                        if (currentObjId != null)
                        {
                            // we want to remove objects that have idRef == objectId of our removed item
                            List<JObject> refRremovalList = new List<JObject>();
                            foreach (JObject otherItems in origJsonJArrayObj)
                            {
                                if (otherItems["Data"]["idRef"] != null)
                                {
                                    if ((int)otherItems["Data"]["idRef"] == (int)currentObjId)
                                    {
                                        refRremovalList.Add(otherItems);
                                    }
                                }
                            }

                            foreach (JObject refObject in refRremovalList)
                            {
                                refObject.AddAfterSelf(nullJObject);
                                refObject.Remove();
                            }
                        }

                        if (CheckIfSuccess(origJsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                        {
                            // it is a success so we can remove it!
                            // we have to start from the beginning!
                            externalCounter = 0;
                            jsonJArrayObj = new JArray(origJsonJArrayObj.ToList().ToArray());
                            sbSuccessResult.AppendLine("Successful in nullifying:" + tempValue);
                        }
                        else
                        {
                            // we should not remove it
                            externalCounter = internalCounter;
                            origJsonJArrayObj = new JArray(jsonJArrayObj.ToList().ToArray());
                        }

                        break;
                    }
                }
            }


            return sbSuccessResult;
        }

        private static StringBuilder RulesRunner(ref JArray jsonJArrayObj, JProperty currentPropItem, string myApp, bool isErrOk, bool showInfo)
        {
            return RulesRunner(ref jsonJArrayObj, currentPropItem, -1, myApp, isErrOk, showInfo);
        }

        private static StringBuilder RulesRunner(ref JArray jsonJArrayObj, JProperty currentPropItem, int arrNum, string myApp, bool isErrOk, bool showInfo)
        {
            StringBuilder sbSuccessResult = new StringBuilder();
            var origCurrentItem = currentPropItem.Value.DeepClone();
            JTokenType currentItemType = currentPropItem.Value.Type;

            if (currentItemType == JTokenType.Array)
            {
                if ((arrNum) > -1)
                {
                    // we are dealing with an array item
                    currentItemType = currentPropItem.Value[arrNum].Type;
                }
            }

            switch (currentItemType)
            {
                case JTokenType.String:
                    string origValue = currentPropItem.Value.ToString();

                    if (arrNum > -1)
                    {
                        origValue = currentPropItem.Value[arrNum].ToString();
                    }

                    // replace a string with null
                    if (arrNum == -1)
                    {
                        currentPropItem.Value = null;
                    }
                    else
                    {
                        currentPropItem.Value[arrNum] = null;
                    }

                    if (CheckIfSuccess(jsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                    {
                        //success
                        sbSuccessResult.AppendLine("String replaced with null: " + origValue);
                        break;
                    }

                    // undo
                    currentPropItem.Value = origCurrentItem.DeepClone();

                    if (!string.IsNullOrEmpty(origValue))
                    {
                        // replace a non empty string with an empty string ('')
                        if (arrNum == -1)
                        {
                            currentPropItem.Value = "";
                        }
                        else
                        {
                            currentPropItem.Value[arrNum] = "";
                        }


                        if (CheckIfSuccess(jsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                        {
                            //success
                            sbSuccessResult.AppendLine("String replaced with empty: " + origValue);
                            break;
                        }

                        // undo
                        currentPropItem.Value = origCurrentItem.DeepClone();

                        if (origValue.Length > 1)
                        {
                            // replace a non empty string greater than one character with one character ('x')
                            if (arrNum == -1)
                            {
                                currentPropItem.Value = "x";
                            }
                            else
                            {
                                currentPropItem.Value[arrNum] = "x";
                            }

                            if (CheckIfSuccess(jsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                            {
                                //success
                                sbSuccessResult.AppendLine("String replaced with 'x': " + origValue);
                                break;
                            }

                            // undo
                            currentPropItem.Value = origCurrentItem.DeepClone();

                            if (origValue.Contains(" "))
                            {
                                // replace space in string if it contains a space    
                                if (arrNum == -1)
                                {
                                    currentPropItem.Value = origValue.Replace(" ", "");
                                }
                                else
                                {
                                    currentPropItem.Value[arrNum] = origValue.Replace(" ", "");
                                }

                                if (CheckIfSuccess(jsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                                {
                                    //success
                                    origValue = currentPropItem.Value.ToString();

                                    if (arrNum > -1)
                                    {
                                        origValue = currentPropItem.Value[arrNum].ToString();
                                    }
                                    sbSuccessResult.AppendLine("Space characters removed from: " + origValue);
                                    // we shouldn't break here! we have things to do!
                                }
                                else
                                {
                                    // undo
                                    currentPropItem.Value = origCurrentItem.DeepClone();
                                }
                            }

                            origCurrentItem = currentPropItem.Value.DeepClone();

                            string newValue = origValue;

                            // replace a full class or assembly string to only keep then class and assembly
                            Regex asmSection = new Regex(@"([^,]+)\s*[,]\s*Version=[^,]+,\s*Culture=[^,]+,\s*PublicKeyToken=[a-z0-9]{16}", RegexOptions.IgnoreCase);

                            foreach (Match match in asmSection.Matches(origValue))
                            {
                                if (arrNum == -1)
                                {
                                    currentPropItem.Value = newValue.Replace(match.Value, match.Groups[1].Value);
                                }
                                else
                                {
                                    currentPropItem.Value[arrNum] = newValue.Replace(match.Value, match.Groups[1].Value);
                                }

                                if (CheckIfSuccess(jsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                                {
                                    // success
                                    newValue = currentPropItem.Value.ToString();
                                    if (arrNum > -1)
                                    {
                                        newValue = currentPropItem.Value[arrNum].ToString();
                                    }
                                    sbSuccessResult.AppendLine("A class or an assembly string became shorter: " + origValue);
                                    origCurrentItem = currentPropItem.Value.DeepClone();
                                }
                            }

                            // undo
                            if (newValue.Equals(origValue))
                            {
                                // failure
                                currentPropItem.Value = origCurrentItem.DeepClone();
                            }
                            else
                            {
                                origValue = newValue;
                            }

                            origCurrentItem = currentPropItem.Value.DeepClone();

                            // replace a full class or assembly string to only keep class
                            Regex classRegex = new Regex(@"([a-z0-9\.\+_\$`]+)\s*[;,]\s*[a-z0-9\.\+_\$`]+", RegexOptions.IgnoreCase);

                            newValue = origValue;

                            foreach (Match match in classRegex.Matches(origValue))
                            {
                                if (arrNum == -1)
                                {
                                    currentPropItem.Value = newValue.Replace(match.Value, match.Groups[1].Value);
                                }
                                else
                                {
                                    currentPropItem.Value[arrNum] = newValue.Replace(match.Value, match.Groups[1].Value);
                                }

                                if (CheckIfSuccess(jsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                                {
                                    // success
                                    newValue = currentPropItem.Value.ToString();
                                    if (arrNum > -1)
                                    {
                                        newValue = currentPropItem.Value[arrNum].ToString();
                                    }
                                    sbSuccessResult.AppendLine("A class or an assembly string became shorter: " + origValue);
                                    origCurrentItem = currentPropItem.Value.DeepClone();
                                }
                            }

                            // undo if failed
                            if (arrNum == -1 && !newValue.Equals(currentPropItem.Value.ToString()))
                            {
                                currentPropItem.Value = origCurrentItem.DeepClone();
                            }
                            else if (arrNum > -1 && !newValue.Equals(currentPropItem.Value[arrNum].ToString()))
                            {
                                currentPropItem.Value = origCurrentItem.DeepClone();
                            }

                        }
                    }

                    break;
                case JTokenType.Integer:
                    // replace an integer with 0 to N - when int is M and N < M && N < 20 (we need a limit)
                    var origItemValue = currentPropItem.Value.DeepClone();
                    int origIntValue = 0;

                    if (arrNum == -1)
                    {
                        int.TryParse(currentPropItem.Value.ToString(), out origIntValue);
                    }
                    else
                    {
                        int.TryParse(currentPropItem.Value[arrNum].ToString(), out origIntValue);
                    }


                    if (origIntValue > 0)
                    {
                        int intCounter = 0;

                        while (intCounter < origIntValue && intCounter < 20)
                        {

                            if (arrNum == -1)
                            {
                                currentPropItem.Value = intCounter;
                            }
                            else
                            {
                                currentPropItem.Value[arrNum] = intCounter;
                            }

                            if (CheckIfSuccess(jsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                            {
                                //success
                                sbSuccessResult.AppendLine("A number was changed from: " + origIntValue + " to: " + intCounter);
                                break;
                            }
                            else
                            {
                                currentPropItem.Value = origItemValue.DeepClone();
                            }

                            intCounter++;
                        }
                    }
                    break;
                default:
                    // convert to null
                    currentPropItem.Value = null;
                    if (CheckIfSuccess(jsonJArrayObj.ToString(), myApp, isErrOk, showInfo))
                    {
                        //success
                        sbSuccessResult.AppendLine("An item was replaced with null: " + origCurrentItem.ToString());
                        break;
                    }
                    // undo
                    currentPropItem.Value = origCurrentItem.DeepClone();
                    break;
            }

            return sbSuccessResult;
        }

        private static bool CheckIfSuccess(string strJson, string myApp, bool isErrOk, bool showInfo)
        {
            bool result = true;

            try
            {
                if (!BinaryFormatterDeserializeABFJson(strJson, showInfo))
                {
                    if (!isErrOk)
                    {
                        // we have error but we don't like errors
                        result = false;
                    }
                }
            }
            catch
            {
                if (!isErrOk)
                {
                    // we have error but we don't like errors
                    result = false;
                }
            }


            if (!KillMyProcess(myApp))
            {
                // no app was found so the code exec did not work
                result = false;
            }

            return result;
        }
        public static object BinaryFormatter_deserialize(byte[] byteArray)
        {
            MemoryStream ms = new MemoryStream(byteArray);
            return BinaryFormatter_deserialize(ms);
        }
        public static object BinaryFormatter_deserialize(MemoryStream ms)
        {
            ms.Position = 0;
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            return bf.Deserialize(ms);
        }
        private static bool BinaryFormatterDeserializeABFJson(string strJson, bool showInfo)
        {
            bool noError = true;
            try
            {
                MemoryStream ms = AdvancedBinaryFormatterParser.JsonToStream(strJson);

                /*
                ms.Position = 0;
                BinaryFormatter bf = new BinaryFormatter();
                var task = Task.Run(() => bf.Deserialize(ms));              
                //*/

                var task = Task.Run(() => { try { BinaryFormatter_deserialize(ms.ToArray()); } catch (Exception e) { noError = false; } });

                if (!task.Wait(TimeSpan.FromSeconds(5)))
                {
                    noError = false;
                    if(showInfo)
                        Console.WriteLine("The formatter is not responding - infinite loop because of parameters.");
                }



            }
            catch (Exception e)
            {
                noError = false;
            }

            return noError;
        }

        private static bool KillMyProcess(string myprocess)
        {
            bool processFound = false;
            foreach (Process myp in Process.GetProcessesByName(myprocess))
            {
                // It has worked
                processFound = true;
                // killing any existing TestConsoleApp_YSONET to be ready
                try
                {
                    myp.Kill();
                }
                catch
                {
                    // hopefully it is just a race condition and all has been closed!!!
                    // just to be on the safe side:
                    processFound = KillMyProcess(myprocess);
                }

            }

            return processFound;
        }
    }
}
