# Error Log

Use this file to track build/runtime errors, test failures, and debugging notes.

Format suggestion:

- Date: YYYY-MM-DD
- File/Project: <path or project name>
- Error: <short error message>
- Details: <stack trace or steps to reproduce>
- Status: open/closed

Entries (newest first):

- Date: 2026-04-12
- File/Project: BIMIntelligence (build)
- Error: Microsoft.VisualBasic version conflict between .NET ref pack and Revit assemblies
- Details: Found conflicts between different versions of "Microsoft.VisualBasic" that could not be resolved. There was a conflict between "Microsoft.VisualBasic, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a" and "Microsoft.VisualBasic, Version=10.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a".
    - "Microsoft.VisualBasic, Version=10.0.0.0" was chosen because it was primary and "Microsoft.VisualBasic, Version=10.1.0.0" was not.
    - References depending on 10.0.0.0: C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.5\ref\net10.0\Microsoft.VisualBasic.dll
    - References depending on or unified to 10.1.0.0: Revit 2027 assemblies under C:\Program Files\Autodesk\Revit 2027\ (RevitAPI, RevitAPIUI, RevitDBAPI, etc.).
  This can cause runtime/type mismatches or build warnings/errors.
- Action: Align referenced Microsoft.VisualBasic versions: target a compatible .NET runtime, add explicit package/reference to match Revit assemblies, or use binding redirects if applicable. Status: open

- Date: 2026-04-12
- File/Project: BIMIntelligence/Services/ChatService.cs
- Error: NullReferenceException when sending message
- Details: Occurs when ChatService.Send is called with null message; stack trace...
- Status: open

- Date: 2026-04-12
- File/Project: BIMIntelligence (build)
- Error: Assembly version mismatch: RevitAPIUI depends on System.Runtime v10.0.0.0, but referenced System.Runtime is v8.0.0.0
- Details: Assembly 'RevitAPIUI' with identity 'RevitAPIUI, Version=27.0.10.0, Culture=neutral, PublicKeyToken=null' uses 'System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' which has a higher version than referenced assembly 'System.Runtime' with identity 'System.Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
- Original message: "Assembly 'RevitAPIUI' with identity 'RevitAPIUI, Version=27.0.10.0, Culture=neutral, PublicKeyToken=null' uses 'System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' which has a higher version than referenced assembly 'System .Runtime' with identity 'System .Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'"
- Action: Investigate referenced Revit assemblies and project target framework; consider aligning System.Runtime versions by upgrading target framework, updating package references, or adding assembly binding redirects. Status: open

Add new entries at the top so the most recent issues are easy to find.
