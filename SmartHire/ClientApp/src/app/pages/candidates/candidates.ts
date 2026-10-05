import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CandidateSummary, CandidatesService } from '../../core/services/candidates.service';

@Component({
  selector: 'app-candidates',
  imports: [CommonModule, RouterLink],
  templateUrl: './candidates.html',
  styleUrl: './candidates.scss',
})
export class Candidates implements OnInit {
  readonly candidates = signal<CandidateSummary[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  constructor(private readonly candidatesService: CandidatesService) {}

  ngOnInit(): void {
    this.candidatesService.getAll().subscribe({
      next: (candidates) => {
        this.candidates.set(candidates);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load candidates.');
        this.loading.set(false);
      },
    });
  }
}
