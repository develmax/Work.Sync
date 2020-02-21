using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WorkSync
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var dflow = new BlockingCollection<f>();
            var rflow = new BlockingCollection<f>();
            var wflow = new BlockingCollection<f>();
            var tflow = new BlockingCollection<f>();
            var sflow = new BlockingCollection<f>();

            var ps = new BlockingCollection<p>();
            var ts = new BlockingCollection<t>();

            for (int j = 0; j < 2; j++)
            {
                ps.Add(new p());
                ts.Add(new t());
                var f = new f {name = "f" + (j+1)};

                if (j == 0)
                {
                    f.devsec = 3;
                    f.writesec = 2;
                    f.revsec = 2;
                    f.revcount = 2;
                    f.testsec = 2;
                    f.testcount = 3;
                }
                else
                {
                    f.devsec = 5;
                    f.writesec = 3;
                    f.revsec = 3;
                    f.revcount = 3;
                    f.testsec = 3;
                    f.testcount = 2;
                }

                log("tasks: " + (j + 1) + " (" + f.tasks + ")");
                log("time: " + (j + 1) + " (" + f.time + ") sec");

                dflow.Add(f);
                wflow.Add(f);
            }

            log("Start");

            Task dev()
            {
                if (!dflow.TryTake(out var f))
                    return null;

                if (f.c == null)
                {
                    if (!ps.TryTake(out var p))
                    {
                        dflow.Add(f);
                    }
                    else
                    {
                        log("dev: " + f.name + " (1)");
                        return p.dev(f, 3).ContinueWith(t =>
                        {
                            rflow.Add(f);
                            ps.Add(p);
                        });
                    }
                }
                else
                {
                    if (!ps.TryTake(out var p))
                    {
                        dflow.Add(f);
                    }
                    else
                    {
                        if (f.c.p == p)
                        {
                            log("dev: " + f.name + " ("+(f.c.dev+1)+")");
                            return p.dev(f, f.devsec).ContinueWith(t =>
                            {
                                if(f.c.isOk)
                                    tflow.Add(f);
                                else
                                    rflow.Add(f);
                                ps.Add(p);
                            });
                        }
                        else
                        {
                            if (!ps.TryTake(out var p2))
                            {
                                dflow.Add(f);
                                ps.Add(p);
                            }
                            else
                            {
                                log("dev: " + f.name + " (" + (f.c.dev + 1) + ")");
                                ps.Add(p);
                                return p2.dev(f, f.devsec).ContinueWith(t =>
                                {
                                    if (f.c.isOk)
                                        tflow.Add(f);
                                    else
                                        rflow.Add(f);
                                    ps.Add(p2);
                                });
                            }
                        }
                    }
                }

                return null;
            }

            Task rev()
            {
                if (!rflow.TryTake(out var f))
                    return null;

                if (!ps.TryTake(out var p))
                {
                    rflow.Add(f);
                }
                else
                {
                    if (f.c.p != p)
                    {
                        log("rev: " + f.name + " (" + (f.c.rev + 1) + ")");
                        return p.rev(f, f.revsec, f.revcount).ContinueWith(t =>
                        {
                            f.c.isOk = t.Result;
                            if (t.Result)
                            {
                                if (f.a != null)
                                    tflow.Add(f);
                            }
                            else
                                dflow.Add(f);
                            ps.Add(p);
                        });
                    }
                    else
                    {
                        if (!ps.TryTake(out var p2))
                        {
                            rflow.Add(f);
                            ps.Add(p);
                        }
                        else
                        {
                            log("rev: " + f.name + " (" + (f.c.rev + 1) + ")");
                            ps.Add(p);
                            return p2.rev(f, f.revsec, f.revcount).ContinueWith(t =>
                            {
                                f.c.isOk = t.Result;
                                if (t.Result)
                                {
                                    if(f.a != null)
                                        tflow.Add(f);
                                }
                                else
                                    dflow.Add(f);
                                ps.Add(p2);
                            });
                        }
                    }
                }

                return null;
            }

            Task write()
            {
                if (!wflow.TryTake(out var f))
                    return null;

                if (!ts.TryTake(out var t))
                {
                    wflow.Add(f);
                }
                else
                {
                    log("write: " + f.name);
                    return t.write(f, f.writesec).ContinueWith(t0 =>
                    {
                        if (f.c?.isOk ?? false)
                            tflow.Add(f);
                        ts.Add(t);
                    });
                }

                return null;
            }

            Task test()
            {
                if (!tflow.TryTake(out var f))
                    return null;

                if (!ts.TryTake(out var t))
                {
                    tflow.Add(f);
                }
                else
                {
                    log("test: " + f.name + " (" + (f.a.run + 1) + ")");
                    return t.test(f, f.testsec, f.testcount).ContinueWith(t0 =>
                    {
                        f.a.isOk = t0.Result;
                        if(t0.Result)
                            sflow.Add(f);
                        else
                            dflow.Add(f);
                        ts.Add(t);
                    });
                }

                return null;
            }

            //List<Task> TaskList = new List<Task>();
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            while (sflow.Count < 2)
            {
                var d = dev();
                var r = rev();
                var w = write();
                var t = test();
                //log("loop");
                //Task.WaitAll(d, r, w, t);

                /*if(d != null)
                    TaskList.Add(d);
                if (r != null)
                    TaskList.Add(r);
                if (w != null)
                    TaskList.Add(w);
                if (t != null)
                    TaskList.Add(t);
                    */
                /*if (d != null) await d;
                if (r != null) await r;
                if (w != null) await w;
                if (t != null) await t;*/
                //Task.WaitAll(d,r,w,t);
            }

           // Task.WaitAll(TaskList.ToArray());
            stopWatch.Stop();
            log("End");
            //log("tasks: "+TaskList.Count);

            while (sflow.TryTake(out var fx))
            log("time fact: " + (fx.name) + " (" + fx.timefact + ") sec");

            var tsd = stopWatch.Elapsed;
            var elapsedTime = $"{tsd.Hours:00}:{tsd.Minutes:00}:{tsd.Seconds:00}.{tsd.Milliseconds / 10:00}";
            log("RunTime " + elapsedTime);

            pause();
        }

        static void log(string text)
        {
            Console.WriteLine(text);
        }

        static void pause()
        {
            Console.ReadLine();
        }

        class p
        {
            public async Task<c> dev(f f, int sec)
            {
                await Task.Delay(TimeSpan.FromSeconds(sec));
                f.timefact += sec;
                var res = f.c ?? (f.c = new c { p = this});

                f.c.dev++;

                return res;
            }

            public async Task<bool> rev(f f, int sec, int rep)
            {
                await Task.Delay(TimeSpan.FromSeconds(sec));
                f.timefact += sec;
                f.c.rev++;

                var res = f.c.rev >= rep;

                return res;
            }
        }

        class t
        {
            public async Task<a> write(f f, int sec)
            {
                await Task.Delay(TimeSpan.FromSeconds(sec));
                f.timefact += sec;
                var res = f.a ?? (f.a = new a());

                return res;
            }

            public async Task<bool> test(f f, int sec, int rep)
            {
                await Task.Delay(TimeSpan.FromSeconds(sec));
                f.timefact += sec;
                f.a.run++;

                var res = f.a.run >= rep;

                return res;
            }
        }

        class c
        {
            public p p;
            public bool isOk;
            public int rev;
            public int dev;
        }

        class a
        {
            public int run;
            public bool isOk;
        }

        class f
        {
            public string name;
            public c c;
            public a a;

            public int devsec;
            public int writesec;
            public int revsec;
            public int testsec;

            public int revcount;
            public int testcount;

            public int timefact;

            public int tasks => 1 + revcount+ (revcount - 1) + testcount + (testcount - 1);
            public int time => System.Math.Max(devsec, writesec) +
                               revsec * revcount + 
                               devsec * (revcount - 1) + 
                               testsec * testcount +
                               devsec * (testcount - 1);
        }
    }
}
