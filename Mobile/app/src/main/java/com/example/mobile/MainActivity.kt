package com.example.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.Event
import androidx.compose.material.icons.filled.Receipt
import androidx.compose.material3.Icon
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.adaptive.navigationsuite.NavigationSuiteScaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.tooling.preview.PreviewScreenSizes
import com.example.mobile.ui.theme.MobileTheme

enum class UserRole {
    NONE,
    ADMIN,
    PUBLIC
}
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            MobileTheme {
                MobileApp()
            }
        }
    }
}

@PreviewScreenSizes
@Composable
fun MobileApp() {
    var userRole by rememberSaveable { mutableStateOf(UserRole.NONE) }

    if (userRole == UserRole.NONE) {
        LoginScreen(onLogin = { role -> userRole = role })
    } else {
        var currentDestination by rememberSaveable { mutableStateOf(AppDestinations.DATLICH) }
        val filteredDestinations = AppDestinations.entries.filter { 
            it.role == userRole || it.role == UserRole.NONE
        }

        NavigationSuiteScaffold(
            navigationSuiteItems = {
                filteredDestinations.forEach {
                    item(
                        icon = {
                            Icon(
                                it.icon,
                                contentDescription = it.label
                            )
                        },
                        label = { Text(it.label) },
                        selected = it == currentDestination,
                        onClick = { currentDestination = it }
                    )
                }
            }
        ) {
            Scaffold(modifier = Modifier.fillMaxSize()) { innerPadding ->
                Greeting(
                    name = currentDestination.label,
                    modifier = Modifier.padding(innerPadding)
                )
            }
        }
    }
}

enum class AppDestinations(
    val label: String,
    val icon: ImageVector,
    val role: UserRole
) {
    DATLICH("Đặt lịch", Icons.Filled.CalendarToday, UserRole.PUBLIC),
    XEMLICH("Xem lịch", Icons.Filled.Event, UserRole.PUBLIC),
    XEMDON("Xem đơn", Icons.Filled.Receipt, UserRole.ADMIN)
}

@Composable
fun Greeting(name: String, modifier: Modifier = Modifier) {
    Text(
        text = "Welcome to the $name screen!",
        modifier = modifier
    )
}

@Preview(showBackground = true)
@Composable
fun GreetingPreview() {
    MobileTheme {
        MobileApp()
    }
}
