using System;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json.Linq;
using DomainObjects;

namespace Loader
{

    [AttributeUsage(validOn: AttributeTargets.Property | AttributeTargets.Method)]
    public class MetaInfoAttribute : Attribute {
        public MetaInfoAttribute(int publicId, string description) {
            PublicId = publicId;
            Description = description;
        }

        public int PublicId { get; set; }
        public string Description { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var assembly = Assembly.Load("DomainObjects");
            var typeofAction = assembly.GetType("DomainObjects.Action");

            var actionProperty = typeof(DomainObjects.Action).GetProperty("Scheduled");

            var domainObjectsName = new AssemblyName("DomainObjects.Annotated");
            var domainObjectAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(domainObjectsName, AssemblyBuilderAccess.RunAndCollect);
            var domainObjectsModuleBuilder = domainObjectAssemblyBuilder.DefineDynamicModule($"{domainObjectsName.Name}.dll");

            var metaAttributeConstructor = typeof(MetaInfoAttribute).GetConstructors()[0];

            var metaAttributeBuilder = new CustomAttributeBuilder(
                metaAttributeConstructor, new object[] {
                    5000002,
                    "The scheduled date of the action."
                });


            var actionTypeBuilder = domainObjectsModuleBuilder.DefineType(
                "DomainObjects.Annotated.Action",
                TypeAttributes.Public,
                typeofAction);

            var scheduledGetPassthrough = actionTypeBuilder.DefineMethod("get_Scheduled", MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, typeof(DateTime?), Type.EmptyTypes);
            var ilGet = scheduledGetPassthrough.GetILGenerator();
            ilGet.Emit(OpCodes.Ldarg_0);
            ilGet.EmitCall(OpCodes.Call, actionProperty.GetGetMethod(), null);
            ilGet.Emit(OpCodes.Ret);
            scheduledGetPassthrough.SetCustomAttribute(metaAttributeBuilder);

            var scheduledSetPassthrough = actionTypeBuilder.DefineMethod("set_Scheduled", MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, null, new [] { typeof(DateTime?) });
            var ilSet = scheduledSetPassthrough.GetILGenerator();
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldarg_1);
            ilSet.EmitCall(OpCodes.Call, actionProperty.GetSetMethod(), null);
            ilSet.Emit(OpCodes.Ret);
            scheduledSetPassthrough.SetCustomAttribute(metaAttributeBuilder);

            var typeofActionMeta = actionTypeBuilder.CreateType();


            // try it out!
            DomainObjects.Action actionInstance = (DomainObjects.Action)domainObjectAssemblyBuilder.CreateInstance("DomainObjects.Annotated.Action");
            actionInstance.ID = 1234;
            actionInstance.Type = 12;
            actionInstance.Scheduled = new DateTime(2020, 02, 29);
            actionInstance.Entered = new DateTime(2020, 02, 19);
            actionInstance.Comment = "N/A";

            var jActionInstance = JObject.FromObject(actionInstance);
            var scheduleMetaInfo = actionInstance.GetType().GetMethod("get_Scheduled").GetCustomAttribute(typeof(MetaInfoAttribute));
            Console.WriteLine(scheduleMetaInfo == null);
            jActionInstance.Add("Scheduled$meta", JObject.FromObject(scheduleMetaInfo));
            Console.WriteLine(jActionInstance);
        }
    }
}
