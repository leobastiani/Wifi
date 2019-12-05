using System;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace Wifi
{
    class Program
    {
        private static string NOME;
        private static string SENHA;
        private static string startUpPath = Environment.GetEnvironmentVariable("appdata")+"/Microsoft/Windows/Start Menu/Programs/Startup/Iniciar Wi-fi.bat";

        static void Main(string[] args)
        {
            //wifi "^.^" "carinhafeliz" "Wi-Fi" "Ethernet"
            //string[] newArgs = { @"^.^", "carinhafeliz", "Wi-Fi", "Ethernet" };
            //string[] newArgs = {"^.^", "carinhafeliz", "Wi-Fi", "Ethernet", @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0000"};
            //args = newArgs;


            if (args.Length == 1)
            {
                if (args[0] == "stop")
                {
                    execCmd("netsh wlan stop hostednetwork");
                    sair();
                }
                if (args[0] == "delete")
                {
                    System.IO.File.Delete(startUpPath);
                    sair();
                }
                

                // devo reiniciar a tarefa
                execCmd('"'+startUpPath+'"', true);
                sair();
            }


            else if (args.Length != 4 && args.Length != 5)
            {
                Console.WriteLine("usage: ./wifi \"NomeDaRede\" \"Senha\" \"NomeDoAdaptadorWifi\" \"NomeDoAdapCabo\"");
                Console.WriteLine("Precisa do devcon e iniciar em modo administrador");
                Console.WriteLine("Adaptadores:\n");

                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    Console.WriteLine(ni.Name);
                }

                sair();
            }

            else
            {
                NOME = args[0];
                Console.WriteLine("Nome da rede: \"" + NOME + "\"");
                SENHA = args[1];
                var adapNameWifi = args[2];
                var adapNameCabo = args[3];

                // agora que já tenho o nome, devo conferir se ele está de pé ou não
                NetworkInterface networkInterfaceWifi = getAdaptByName(adapNameWifi);
                NetworkInterface networkInterfaceCabo = getAdaptByName(adapNameCabo);

                Console.WriteLine("Verificando se está no cabo.");
                bool isWireless = networkInterfaceCabo.OperationalStatus == OperationalStatus.Down;
                bool isCable = !isWireless;
                Console.WriteLine("Status do cabo: "+isCable);

                if (args.Length == 4)
                {
                    // o networkInterface.Description deve ser igual ao registro de DriverDesc
                    // ou o Id igual ao NetCfgInstanceId
                    // os registro estão da seguinte forma
                    // registros
                    //   + Pasta 1
                    //     + Pasta 2
                    //       + NetCfgInstanceId
                    var NetCfgInstanceId = "NetCfgInstanceId";

                    // aqui estará td os roles pra dps do foreach
                    string pastaRegistroFinal = null;

                    string registros = @"SYSTEM\CurrentControlSet\Control\Class";
                    pastaRegistroFinal = getPastaRegistroFinal(networkInterfaceWifi, NetCfgInstanceId, registros);
                    if (string.IsNullOrEmpty(pastaRegistroFinal))
                    {
                        Console.WriteLine("Erro");
                        sair();
                    }

                    // neste ponto
                    // pastaRegistroFinal é algo do tipo:
                    // @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0000"
                    // vamos agora pegar o valor do WirelessMode
                    Console.WriteLine("Nome da rede: \""+NOME+"\"");
                    Console.WriteLine("Senha: \""+SENHA+"\"");

                    // Compose a string that consists of three lines.
                    string startupContent = String.Format(
@"@echo off

if [%1]==[] (
	wifi ""{0}"" ""{1}"" ""{2}"" ""{3}"" ""{4}""
	goto:EOF
)


IF [%1]==[onstart] (
	rem echo onstart
	goto:EOF
)


IF [%1]==[onstop] (
	rem echo onstop
	goto:EOF
)
", NOME, SENHA, adapNameWifi, adapNameCabo, pastaRegistroFinal);

                    // Write the string to a file.
                    System.IO.StreamWriter file = new System.IO.StreamWriter(startUpPath);
                    file.WriteLine(startupContent);

                    file.Close();

                    Console.WriteLine("Tarefa criada!");
                    sair();
                }

                else if (args.Length == 5)
                {
                    var pastaRegistroFinal = args[4];
                    Console.WriteLine("Verificando o WirelessMode.");
                    object keyWirelessMode = Registry.LocalMachine.OpenSubKey(pastaRegistroFinal).GetValue("WirelessMode");
                    bool temWirelessMode = keyWirelessMode != null;
                    int wirelesMode = temWirelessMode ? int.Parse(Convert.ToString(keyWirelessMode)) : 0;
                    Console.WriteLine("Verificando o WirelessMode: "+wirelesMode);
                    try
                    {
                        // agora vamos testar se eh condizente
                        if (wirelesMode == 0 && isCable)
                        {
                            // só inicio o wifi
                            wifiStart();
                            execCmd('"' + startUpPath + "\" onstart", true);
                        }
                        else if (wirelesMode != 0 && isCable)
                        {
                            // eu estava no wifi
                            // agora eu to no cabo
                            setWirelesMode(pastaRegistroFinal, "0");
                            // nao consigo reinicar o wifi e iniciar o share mto rapidamente
                            System.Threading.Thread.Sleep(3000);
                            wifiStart();
                            execCmd('"' + startUpPath + "\" onstart", true);
                        }
                        else if (temWirelessMode && wirelesMode == 0 && isWireless)
                        {
                            // nao preciso iniciar o wifi
                            // mas devo deixar o wifi com wirelesMode de 32
                            setWirelesMode(pastaRegistroFinal, "32");
                            execCmd('"' + startUpPath + "\" onstop", true);
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Acesso negado.");
                        sair();
                    }
                }
            }



            sair();
        }

        private static void sair()
        {
#if DEBUG
            Console.WriteLine("Sair");
            Console.ReadLine();
#endif
            Environment.Exit(0);
        }

        private static string quoteEncapsule(string s)
        {
            // retorna algo como:
            // """s"""
            if (s == "^.^")
            {
                // caso especial
                s = "\"^.^\"";
            }
            return "\"\"\"" + s + "\"\"\"";
        }

        private static NetworkInterface getAdaptByName(string adapNameWifi)
        {
            Console.WriteLine("Obtendo a interface do adaptador \""+adapNameWifi+"\"");
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.Name == adapNameWifi)
                {
                    return ni;
                }
            }
            Console.WriteLine("Não foi encontrado um adaptador com o nome: " + adapNameWifi);
            sair();
            return null;
        }

        private static void setWirelesMode(string pastaRegistroFinal, string v)
        {
            // wirelessMode de fato
            string keyName = "WirelessMode";
            definirKey(pastaRegistroFinal, keyName, v);
            // devo reiniciar o wifi
            var DeviceInstanceID = Convert.ToString(Registry.LocalMachine.OpenSubKey(pastaRegistroFinal).GetValue("DeviceInstanceID"));
            execCmd("devcon disable \"@" + DeviceInstanceID + "\"");
            execCmd("devcon enable \"@" + DeviceInstanceID + "\"");
        }

        private static void definirKey(string registroPath, string keyName, string valor)
        {
            Registry.LocalMachine.OpenSubKey(registroPath, true).SetValue(keyName, valor);
            Console.WriteLine("Registro: '" + registroPath + "' defino keyName: '" + keyName + "' para: " + valor);
        }

        private static void wifiStart()
        {
            // iniciar o wifi de fato
            Console.WriteLine("Função wifiStart.");
            execCmd("netsh wlan set hostednetwork mode=allow ssid=\"" + NOME + "\" key=\"" + SENHA + "\"");
            execCmd("netsh wlan start hostednetwork");
        }

        private static string getPastaRegistroFinal(NetworkInterface networkInterface, string NetCfgInstanceId, string registros)
        {
            var pastas1 = Registry.LocalMachine.OpenSubKey(registros).GetSubKeyNames();
            // para cada registro
            foreach (var pasta1 in pastas1)
            {
                var pastas2 = Registry.LocalMachine.OpenSubKey(registros + "\\" + pasta1).GetSubKeyNames();
                foreach (var pasta2 in pastas2)
                {
                    // devo encontrar o NetCfgInstanceId
                    try
                    {
                        string possivelRegistroFinal = registros + "\\" + pasta1 + "\\" + pasta2;
                        string registroValor = Convert.ToString(Registry.LocalMachine.OpenSubKey(possivelRegistroFinal).GetValue(NetCfgInstanceId));
                        if (registroValor != "")
                        {
                            if (registroValor == networkInterface.Id)
                            {
                                return possivelRegistroFinal;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }

            return null;
        }

        private static void execCmd(string command, bool showWindow = false)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = "/C " + command;
#if !DEBUG
            if (!showWindow)
            {
                process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            }
#else
            process.StartInfo.Arguments += " && pause";
#endif
            Console.WriteLine(process.StartInfo.FileName + " " + process.StartInfo.Arguments);
            process.Start();
            process.WaitForExit();
        }
    }
}
