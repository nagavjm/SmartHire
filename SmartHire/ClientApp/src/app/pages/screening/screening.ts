import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CandidateMatch, ScreeningService } from '../../core/services/screening.service';

interface ChatHistoryItem {
  id: string;
  title: string;
}

interface FeatureCard {
  icon: string;
  title: string;
  description: string;
}

interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  candidates?: CandidateMatch[];
}

@Component({
  selector: 'app-screening',
  imports: [CommonModule, FormsModule],
  templateUrl: './screening.html',
  styleUrl: './screening.scss',
})
export class Screening {
  readonly chatHistory = signal<ChatHistoryItem[]>([]);
  readonly messageText = signal('');
  readonly messages = signal<ChatMessage[]>([]);
  readonly loading = signal(false);

  constructor(private readonly screeningService: ScreeningService) {}

  readonly featureCards: FeatureCard[] = [
    {
      icon: 'dashboard',
      title: 'Resume Screening',
      description: 'Match resumes against a job description using AI ranking.',
    },
    {
      icon: 'search',
      title: 'JD-Based Grounding',
      description: 'Ground candidate analysis in the job description context.',
    },
    {
      icon: 'candidate',
      title: 'Candidate Insights',
      description: 'Get skill gaps, highlights, and fit summaries instantly.',
    },
    {
      icon: 'report',
      title: 'Export Reports',
      description: 'Generate shortlists and export screening results.',
    },
  ];

  sendMessage(): void {
    const text = this.messageText().trim();
    if (!text || this.loading()) {
      return;
    }
    this.messageText.set('');
    this.messages.update((msgs) => [...msgs, { role: 'user', text }]);
    this.loading.set(true);

    this.screeningService
      .rank({
        jobTitle: 'Screening Request',
        jobDescription: text,
        requiredSkills: '',
        minimumExperienceYears: 0,
      })
      .subscribe({
        next: (outcome) => {
          this.messages.update((msgs) => [
            ...msgs,
            {
              role: 'assistant',
              text: outcome.candidates.length
                ? `Found ${outcome.candidates.length} matching candidate(s):`
                : 'No matching candidates were found.',
              candidates: outcome.candidates,
            },
          ]);
          this.loading.set(false);
        },
        error: () => {
          this.messages.update((msgs) => [
            ...msgs,
            { role: 'assistant', text: 'Something went wrong while screening candidates. Please try again.' },
          ]);
          this.loading.set(false);
        },
      });
  }
}
