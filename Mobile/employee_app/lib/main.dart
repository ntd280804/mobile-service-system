import 'package:flutter/material.dart';

import 'package:flutter/material.dart';
import 'services/storage_service.dart';
import 'screens/login_screen.dart';
import 'screens/home_screen.dart';
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  // This widget is the root of your application.
  @override
    return MaterialApp(
      title: 'Flutter Demo',
      theme: ThemeData(
      title: 'Employee Viewer',
        //
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.blue),
    );
  }
      home: const SplashScreen(),
      routes: {
        '/login': (context) => const LoginScreen(),
        '/home': (context) => const HomeScreen(),
      },

class MyHomePage extends StatefulWidget {
  const MyHomePage({super.key, required this.title});

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});
}

  State<SplashScreen> createState() => _SplashScreenState();
  int _counter = 0;

class _SplashScreenState extends State<SplashScreen> {
  final StorageService _storage = StorageService();

  @override
  void initState() {
    super.initState();
    _checkAuthStatus();
  }

  Future<void> _checkAuthStatus() async {
    await Future.delayed(const Duration(milliseconds: 500));
    
    final token = await _storage.getToken();
    
    if (!mounted) return;
    
    if (token != null) {
      Navigator.of(context).pushReplacementNamed('/home');
    } else {
      Navigator.of(context).pushReplacementNamed('/login');
    }
  Widget build(BuildContext context) {
    // This method is rerun every time setState is called, for instance as done
    // by the _incrementCounter method above.
    //
        // Colors.amber, perhaps?) and trigger a hot reload to see the AppBar
        child: Column(
          // children horizontally, and tries to be as tall as its parent.
          //
          // Column has various properties to control how it sizes itself and
            Icon(
              Icons.business,
              size: 80,
              color: Theme.of(context).primaryColor,
          // axis because Columns are vertical (the cross axis would be
            const SizedBox(height: 20),
            const Text(
              'Employee Viewer',
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 20),
            const CircularProgressIndicator(),
          ],
        ),
      ),
    );
  }
}

          // action in the IDE, or press "p" in the console), to see the
          // wireframe for each widget.
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            const Text('You have pushed the button this many times:'),
            Text(
              '$_counter',
              style: Theme.of(context).textTheme.headlineMedium,
            ),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: _incrementCounter,
        tooltip: 'Increment',
        child: const Icon(Icons.add),
      ), // This trailing comma makes auto-formatting nicer for build methods.
    );
  }
}
