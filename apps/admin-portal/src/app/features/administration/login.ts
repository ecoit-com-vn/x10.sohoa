import { Component } from '@angular/core';
import { Card } from 'primeng/card';
import { InputText } from 'primeng/inputtext';
import { Password } from 'primeng/password';
import { Button } from 'primeng/button';
import { Router } from '@angular/router';

@Component({
  selector: 'app-login',
  imports: [Card, InputText, Password, Button],
  template: `
    <div class="login-wrapper">
      <p-card header="EVNHANOI Login" [style]="{ width: '400px' }">
        <div style="display: flex; flex-direction: column; gap: 1rem;">
          <div>
            <label for="username" style="display: block; margin-bottom: 0.5rem; font-weight: 500;">Username</label>
            <input pInputText id="username" style="width: 100%" />
          </div>
          <div>
            <label for="password" style="display: block; margin-bottom: 0.5rem; font-weight: 500;">Password</label>
            <p-password id="password" [toggleMask]="true" styleClass="w-full" [inputStyle]="{'width':'100%'}" [feedback]="false"></p-password>
          </div>
          <p-button label="Login" (onClick)="onLogin()" styleClass="w-full" [style]="{'width':'100%', 'margin-top':'0.5rem'}"></p-button>
        </div>
      </p-card>
    </div>
  `,
  styles: `
    .login-wrapper {
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100vh;
      background-color: var(--p-surface-50);
    }
  `,
})
export class Login {
  constructor(private router: Router) {}
  
  onLogin() {
    this.router.navigate(['/']);
  }
}
