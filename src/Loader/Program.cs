using System;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json.Linq;

namespace Loader
{

    [AttributeUsage(validOn: AttributeTargets.Property)]
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

            // 1. Extract interface from target type
            // 2. Make new type that extends target type and implements interface from 1
            // (Note: Method implementation _must_ include virtual method call to base type.)

            /*
                interface MetaInfo {
                    FQN: {
                        Name: string; // text doc or POCO model
                        Resolver: string; // type name
                    },
                    Name: string;
                    Interpretation: string;
                    Attributes: string[];
                    ViewLocations: IDictionary<string, IViewLocation>;
                }

                interface IViewLocation {
                    // Implementation specific somehow.
                    // locate this data on a RA form
                    // OR
                    // locate this data in workflow    
                }
            
            */

            // define assembly for annotated types
            var domainObjectsName = new AssemblyName("DomainObjects.Annotated");
            var domainObjectAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(domainObjectsName, AssemblyBuilderAccess.RunAndCollect);
            var domainObjectsModuleBuilder = domainObjectAssemblyBuilder.DefineDynamicModule($"{domainObjectsName.Name}.dll");

            // generate interface with annotation attributes
            var actionInterfaceBuilder = domainObjectsModuleBuilder.DefineType("DomainObjects.Annotated.IAction", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
            var scheduledProperty = actionInterfaceBuilder.DefineProperty("Scheduled", PropertyAttributes.HasDefault, typeof(DateTime?), null);

            var metaAttributeConstructor = typeof(MetaInfoAttribute).GetConstructors()[0];

            var metaAttributeBuilder = new CustomAttributeBuilder(
                metaAttributeConstructor, new object[] {
                    5000002,
                    "The scheduled date of the action."
                });

            scheduledProperty.SetCustomAttribute(metaAttributeBuilder);
            var propertyAccessorAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual | MethodAttributes.Abstract;
            var getScheduledMethodBuilder = actionInterfaceBuilder.DefineMethod("get_Scheduled", propertyAccessorAttributes, typeof(DateTime?), Type.EmptyTypes);
            var setScheduledMethodBuilder = actionInterfaceBuilder.DefineMethod("set_Scheduled", propertyAccessorAttributes, null, new [] { typeof(DateTime?) });
            scheduledProperty.SetSetMethod(getScheduledMethodBuilder);
            scheduledProperty.SetSetMethod(setScheduledMethodBuilder);
            var actionInterface = actionInterfaceBuilder.CreateType();

            // generate concretion
            var actionTypeBuilder = domainObjectsModuleBuilder.DefineType(
                "DomainObjects.Annotated.Action",
                TypeAttributes.Public,
                typeofAction, 
                new Type[] { actionInterface });

            var scheduledGetPassthrough = actionTypeBuilder.DefineMethod("get_Scheduled", MethodAttributes.Virtual | MethodAttributes.Private, typeof(DateTime?), Type.EmptyTypes);
            var ilGet = scheduledGetPassthrough.GetILGenerator();
            ilGet.Emit(OpCodes.Ldarg_0);
            ilGet.EmitCall(OpCodes.Callvirt, typeofAction.GetProperty("Scheduled").GetGetMethod(), null);
            ilGet.Emit(OpCodes.Ret);
            actionTypeBuilder.DefineMethodOverride(scheduledGetPassthrough, getScheduledMethodBuilder);

            var scheduledSetPassthrough = actionTypeBuilder.DefineMethod("set_scheduled", MethodAttributes.Virtual | MethodAttributes.Private, null, new [] { typeof(DateTime?) });
            var ilSet = scheduledSetPassthrough.GetILGenerator();
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldarg_1);
            ilSet.EmitCall(OpCodes.Callvirt, typeofAction.GetProperty("Scheduled").GetSetMethod(), null);
            ilSet.Emit(OpCodes.Ret);
            actionTypeBuilder.DefineMethodOverride(scheduledSetPassthrough, setScheduledMethodBuilder);

            var typeofActionMeta = actionTypeBuilder.CreateType();


            // try it out!
            DomainObjects.Action actionInstance = (DomainObjects.Action)domainObjectAssemblyBuilder.CreateInstance("DomainObjects.Annotated.Action");
            actionInstance.ID = 1234;
            actionInstance.Type = 12;
            actionInstance.Scheduled = new DateTime(2020, 02, 29);
            actionInstance.Entered = new DateTime(2020, 02, 19);
            actionInstance.Comment = "N/A";

            // foreach (var info in actionInstance.GetType().GetInterface("IAction").GetProperty("Scheduled").GetCustomAttributes(true)) {
            //     Console.WriteLine(info);
            // }

            // var jsonSettings = new JsonSerializerOptions { };
            // var f = new System.Text.Json.Serialization.JsonConverterFactory().CreateConverter();
            // jsonSettings.Converters.Add()
            // System.Text.Json.Serialization.JsonStringEnumConverter.

            // Console.WriteLine(JsonSerializer.Serialize(actionInstance, actionInstance.GetType()));

            var assemblyGenerator = new Lokad.ILPack.AssemblyGenerator();
            assemblyGenerator.GenerateAssembly(domainObjectAssemblyBuilder, "../../out/DomainObjects.Annotated.dll");

            var jActionInstance = JObject.FromObject(actionInstance);
            var scheduleMetaInfo = actionInstance.GetType().GetInterface("IAction").GetProperty("Scheduled").GetCustomAttribute(typeof(MetaInfoAttribute));
            jActionInstance.Add("Scheduled$meta", JObject.FromObject(scheduleMetaInfo));
            Console.WriteLine(jActionInstance);
        }
    }
}
