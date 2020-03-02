﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

/*
    Server based multiplayer mode, 
    Can be used on Massive Multiplayer games 

    Alert: Insecure for while.
*/

namespace MultiplayerKode
{
    class UDPnetKode
    {
        private UdpClient server;
        private IPEndPoint clientes, broadcast;
        public bool alive = true;
        private int port;
        private int IDcont = 1;
        private List<Client> users = new List<Client>();
        private System.Timers.Timer clock;
        private PackageManager package = new PackageManager();
        private Thread broad;

        /// <summary>
        /// UDP Server for multiplayer games
        /// </summary>
        /// <param name="port">Port for listening</param>
        public UDPnetKode(int port = 8484)
        {
            this.port = port;
            server = new UdpClient(port);
            clientes = new IPEndPoint(IPAddress.Any, port);
            Console.WriteLine("Started Successfully!\n" +
                "Version Alpha 0.0.2");
            clock = new System.Timers.Timer(2000);
            clock.Elapsed += Clock_Elapsed;
            clock.Enabled = true;
            clock.AutoReset = true;
            broad = new Thread(Sync);
            broad.Start();
            Thread messages = new Thread(Mensagens);
            messages.Start();
        }

        /// <summary>
        /// Clock to send ping signal to all players
        /// </summary>
        private void Clock_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            send("",true);
        }

        /// <summary>
        /// Where you receive your messages
        /// </summary>
        public void Mensagens()
        {
            while (alive)
            {
                try
                {
                    byte[] pacote = server.Receive(ref clientes);
                    string[] mensagens = package.Translate(pacote).Split('|');
                    if (mensagens[0] == "Hello")
                    {
                        sendDirect(package.GenerateMessage('|', "HANDSHAKE", IDcont), clientes.Address.ToString(), clientes.Port);
                        users.Add(inserir(clientes.Address.ToString(), clientes.Port, mensagens[1]));
                        send(package.GenerateMessage('|', "INFO", "Join", Procurar(clientes.Address.ToString(), clientes.Port).Id, Procurar(clientes.Address.ToString(), clientes.Port).Nome,2));
                    }
                    else if (mensagens[0] == "bye")
                    {
                        remove(clientes.Address.ToString(), clientes.Port, "Disconnected safely");
                    }
                    else if (mensagens[0] == "pong")
                    {
                        Procurar(clientes.Address.ToString(), clientes.Port).TimeOut = 0;
                    }
                    else if( mensagens[0] == "SYNC")
                    {
                        if (ProcurarPorID(Int32.Parse(mensagens[2])) != null)
                        {
                            Client cliente = ProcurarPorID(Int32.Parse(mensagens[2]));
                            Player jogador = cliente.Player;
                            cliente.SetPositionByString(mensagens[1]);
                        }
                    }
                    else if(mensagens[0] == "CHAT")
                    {
                        if (Procurar(clientes.Address.ToString(), clientes.Port) != null)
                        {
                            Client clien = Procurar(clientes.Address.ToString(), clientes.Port);
                            Console.WriteLine(clien.Nome + ": " + package.Translate(pacote));
                        }
                    }
                    else
                    {
                        if(Procurar(clientes.Address.ToString(),clientes.Port) != null)
                        {
                            Client clien = Procurar(clientes.Address.ToString(), clientes.Port);
                            Console.WriteLine(clien.Nome+": " + package.Translate(pacote)+ "(without use chat command)");
                        }
                        else
                        {
                            Console.WriteLine("Invalid Session!");
                            sendDirect("PleaseConnect", clientes.Address.ToString(), clientes.Port);
                        }
                    }  
                }
                catch (SocketException e)
                {
                    Console.WriteLine("Trying communication.");
                }
            }
        }

        /// <summary>
        /// Sync player's position
        /// </summary>
        public void Sync()
        {
            while (alive)
            {
                for(int j = 0; j < users.Count; j++)
                {
                    try
                    {
                        broadcast = users[j].EndPoint;
                        for (int i = 0; i < users.Count; i++)
                        {
                            if (i != j)
                            {
                                byte[] msg;
                                msg = package.GetBytesFromMessage('|', "SYNC", users[i].GetPosition(), users[i].Id);
                                Console.WriteLine("Sending: "+msg.Length+" Bits.");
                                server.Send(msg, msg.Length, broadcast);
                            }
                        }
                        Thread.Sleep(100);
                    }
                    catch
                    {
                        continue;
                    }
                }
                if(users.Count == 0)
                {
                    Thread.Sleep(50);
                }
            }
        }

        /// <summary>
        /// Send a ping signal to keep connection alive.
        /// </summary>
        /// <param name="aviso">Send a custom message</param>
        public void send(string aviso = "",bool sync = false)
        {
            if(aviso == "EXIT" || aviso == "exit" || aviso == "stop")
            {
                alive = false;
                server.Close();
                clock.Enabled = false;
                clock.AutoReset = false;
            }
            for(int i = 0; i < users.Count; i++)
            {
                if (users[i].TimeOut >= 10)
                {
                    remove(users[i].Address, users[i].Port, "Timed Out");
                }
                else
                {
                    try
                    {
                        broadcast = users[i].EndPoint;
                        byte[] msg;
                        if (sync)
                        {
                            msg = package.GetBytes("ping|" + users[i].Id);
                            server.Send(msg, msg.Length, broadcast);
                            users[i].TimeOut++;
                        }
                        if (aviso.Length > 0)
                        {
                            msg = package.GetBytes(aviso);
                            server.Send(msg, msg.Length, broadcast);
                            Console.WriteLine("Sent: " + aviso);
                        }
                    }
                    catch
                    {
                        remove(users[i].Address, users[i].Port, "Problem");
                    }
                }
            }
        }

        /// <summary>
        /// Search for a user using his address
        /// </summary>
        /// <param name="Address"></param>
        /// <param name="port"></param>
        /// <returns>Client object</returns>
        public Client Procurar(string Address, int port)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].Address == Address && users[i].Port == port)
                {
                    return users[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Search a user by an ID
        /// </summary>
        /// <param name="_id"></param>
        /// <returns>Client object</returns>
        public Client ProcurarPorID(int _id)
        {
            for(int i = 0; i < users.Count; i++)
            {
                if (users[i].Id == _id)
                {
                    return users[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Insert a new client to the list
        /// </summary>
        /// <param name="Address">Client's IP Address.</param>
        /// <param name="_port">Client's port</param>
        /// <param name="nome">Client's name</param>
        /// <param name="qt_custom">Extra sync vars</param>
        /// <returns>Client object</returns>
        public Client inserir(string Address, int _port, string nome)
        {
            Client obj = new Client(Address, _port, nome, IDcont);
            Console.WriteLine("Welcome! " + nome + " with the ID: " + IDcont);
            IDcont++;
            return obj;
        }

        /// <summary>
        /// Send a message directly to an Address
        /// </summary>
        /// <param name="message">String to send to a client</param>
        /// <param name="Address">client's Address</param>
        /// <param name="_port"></param>
        public void sendDirect(string message, string Address, int _port)
        {
            IPEndPoint EP = new IPEndPoint(IPAddress.Parse(Address), _port);
            byte[] msg = package.GetBytes(message);
            server.Send(msg, msg.Length, EP);
            Console.WriteLine("Sending HandShake");
        }

        /// <summary>
        /// removes a client from the list
        /// </summary>
        /// <param name="ip">Client's IP Address</param>
        /// <param name="port">Client's port</param>
        /// <param name="Why">Reason why it is going to be removed</param>
        public void remove(string ip = "", int port = 8484, string Why = "")
        {
            Console.WriteLine("Removing a client by: " + Why);
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].Address == ip && users[i].Port == port)
                {
                    int id = users[i].Id;
                    string nome = users[i].Nome;
                    users.RemoveAt(i);
                    Console.WriteLine("Disconnected: " + nome);
                    send(package.GenerateMessage('|', "INFO", "Left", id, nome));
                    break;
                }
            }
        }

        /// <summary>
        /// removes a client from the list
        /// </summary>
        /// <param name="_ID">Client ID</param>
        /// <param name="Why">Reason why it is going to be removed</param>
        public void remove(int _ID = -1, string Why = "")
        {
            Console.WriteLine("Removing a client by: " + Why);
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].Id == _ID)
                {
                    users.RemoveAt(i);
                }
            }
        }

    }
}