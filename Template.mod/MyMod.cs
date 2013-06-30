using System;

using ScrollsModLoader.Interfaces;
using UnityEngine;
using Mono.Cecil;

namespace Template.mod
{
	public class MyMod : BaseMod
	{

		//initialize everything here, Game is loaded at this point
		public MyMod ()
		{
		}


		public static string GetName ()
		{
			return "TemplateMod";
		}

		public static int GetVersion ()
		{
			return 1;
		}

		//only return MethodDefinitions you obtained through the scrollsTypes object
		//safety first! surround with try/catch and return an empty array in case it fails
		public static MethodDefinition[] GetHooks (TypeDefinitionCollection scrollsTypes, int version)
		{
			return new MethodDefinition[] {};
		}

		
		public override bool BeforeInvoke (InvocationInfo info, out object returnValue)
		{
			returnValue = null;
			return false;
		}

		public override void AfterInvoke (InvocationInfo info, ref object returnValue)
		{
			return;
		}
	}
}

